using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Geo;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Sincroniza las rutas del mapa por FO. Con ruta en Valhalla se guarda su polilinea completa simplificada a
/// <c>Sync__RutaReduceTolMetros</c> (fuente valhalla). Sin ruta en Valhalla el viaje queda SIN ruta, salvo que
/// <c>Sync__RutaDaneFallback</c> este activo: entonces recibe una recta entre los centros poblados DANE (fuente dane).
/// <paramref name="Incremental"/>: solo viajes nuevos o cuya ruta de Valhalla cambio (corrida frecuente); sin la bandera
/// reprocesa todas (corrida diaria de respaldo). Idempotente.
/// </summary>
public sealed record clsSincronizarRutasCommand(bool Incremental = false) : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarRutasCommandHandler(
    IViajesSincronizacionRepository viajes,
    IRutasLegadoPort legado,
    IRutasSincronizacionRepository rutas,
    IOptions<clsSyncOptions> opciones,
    ILogger<clsSincronizarRutasCommandHandler> log)
    : ICommandHandler<clsSincronizarRutasCommand, clsResultadoSincronizacion>
{
    // Lote chico a proposito: se trae la geometria COMPLETA (mucho mas texto por fila que la reducida).
    private const int TAMANO_LOTE = 50;

    private sealed record clsPorProcesar(long ValhallaId, clsViajeParaRutaDto Viaje);

    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarRutasCommand command, CancellationToken ct = default)
    {
        var opts = opciones.Value;
        var lista = await viajes.ListarParaRutasAsync(ct);
        var indice = await legado.ListarIndiceAsync(opts.RutasDesde, ct);

        var valhallaPorFo = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var i in indice) valhallaPorFo[i.Fo] = i.ValhallaId;

        // Varios viajes pueden compartir FO y la ruta es una por FO: se decide por FO para no alternar el viaje enlazado.
        var conValhalla = new List<clsPorProcesar>();
        var sinValhalla = new List<(string Fo, clsViajeParaRutaDto Viaje)>();
        var saltadas = 0;
        foreach (var grupo in lista.GroupBy(v => clsTexto.StripCeros(v.NumeroFo) ?? string.Empty, StringComparer.Ordinal))
        {
            var viaje = grupo.FirstOrDefault(v => v.TieneRuta) ?? grupo.Last();
            if (grupo.Key.Length > 0 && valhallaPorFo.TryGetValue(grupo.Key, out var objetivo))
            {
                var alDia = grupo.Any(v => v.TieneRuta && v.ValhallaIdAlmacenado == objetivo);
                if (command.Incremental && alDia) saltadas++;
                else conValhalla.Add(new clsPorProcesar(objetivo, viaje));
            }
            else
            {
                sinValhalla.Add((grupo.Key, viaje));
            }
        }

        var insertadas = 0;
        var actualizadas = 0;
        var errores = new List<clsErrorSincronizacion>();
        void Acumular(clsResultadoUpsertDto r)
        {
            insertadas += r.Insertados;
            actualizadas += r.Actualizados;
            errores.AddRange(r.Errores);
        }

        var tol = opts.ToleranciaEfectivaMetros;
        var avance = 0;
        foreach (var lote in conValhalla.Chunk(TAMANO_LOTE))
        {
            ct.ThrowIfCancellationRequested();
            IReadOnlyList<clsRutaValhallaDto> geometrias;
            try
            {
                geometrias = await legado.ObtenerGeometriasAsync(lote.Select(l => l.ValhallaId).ToList(), ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                foreach (var l in lote) errores.Add(new clsErrorSincronizacion(l.Viaje.NumeroFo, ex.Message));
                continue;
            }

            var viajePorValhalla = lote.ToDictionary(l => l.ValhallaId, l => l.Viaje.ViajeId);
            var filas = new List<clsRutaUpsertDto>(geometrias.Count);
            foreach (var g in geometrias)
            {
                var polilinea = clsRutaPolilinea.DesdeValhalla(g.Wkt, tol);
                filas.Add(new clsRutaUpsertDto(
                    g.Fo.Trim(), viajePorValhalla.GetValueOrDefault(g.ValhallaId), FuenteRuta.Valhalla, g.ValhallaId,
                    g.Kilometros, polilinea.Count, g.NumPuntosOriginal,
                    clsRutaPolilinea.AJson(polilinea), clsJsonSeguro.NormalizarONull(g.DetalleJson), g.FechaCalculo));
            }
            Acumular(await rutas.UpsertAsync(filas, ct));
            avance += lote.Length;
            log.LogDebug("Rutas Valhalla: avance {Avance}/{Total}", avance, conValhalla.Count);
        }

        var dane = 0;
        if (opts.RutaDaneFallback && sinValhalla.Count > 0)
            dane = await SincronizarDaneAsync(sinValhalla, Acumular, ct);

        log.LogInformation(
            "Sincronizacion de rutas ({Modo}): {Viajes} viajes, {Valhalla} con Valhalla, {Dane} con Dane, {SinRuta} sin ruta, {Saltadas} sin cambios, {Insertadas} insertadas, {Actualizadas} actualizadas, {Errores} errores",
            command.Incremental ? "incremental" : "completo", lista.Count, conValhalla.Count, dane, sinValhalla.Count - dane,
            saltadas, insertadas, actualizadas, errores.Count);

        return clsResultadoSincronizacion.Crear(
            lista.Count, insertadas, actualizadas, saltadas, errores,
            new Dictionary<string, int>
            {
                ["valhalla"] = conValhalla.Count,
                ["dane"] = dane,
                ["sinRuta"] = sinValhalla.Count - dane,
                ["sinCambios"] = saltadas,
            });
    }

    /// <summary>Recta origen-destino con los centros poblados DANE para los viajes sin ruta alguna; nunca pisa una ruta existente.</summary>
    private async Task<int> SincronizarDaneAsync(
        List<(string Fo, clsViajeParaRutaDto Viaje)> sinValhalla, Action<clsResultadoUpsertDto> acumular, CancellationToken ct)
    {
        var candidatos = sinValhalla.Where(s => s.Fo.Length > 0 && !s.Viaje.TieneRuta).ToList();
        if (candidatos.Count == 0) return 0;

        var centros = await legado.ListarCentrosDaneAsync(ct);
        var filas = new List<clsRutaUpsertDto>();
        foreach (var (fo, viaje) in candidatos)
        {
            var origen = clsTexto.NormalizarCodigoDane(viaje.CiudadOrigen);
            var destino = clsTexto.NormalizarCodigoDane(viaje.CiudadDestino);
            if (origen is null || destino is null) continue;
            if (!centros.TryGetValue(origen, out var o) || !centros.TryGetValue(destino, out var d)) continue;

            var puntos = clsRutaPolilinea.DesdeDane(new clsCoordenada(o.Lat, o.Lng), new clsCoordenada(d.Lat, d.Lng));
            filas.Add(new clsRutaUpsertDto(
                fo, viaje.ViajeId, FuenteRuta.Dane, null, clsRutaPolilinea.Kilometros(puntos), puntos.Count, puntos.Count,
                clsRutaPolilinea.AJson(puntos), null, null));
        }
        if (filas.Count > 0) acumular(await rutas.UpsertAsync(filas, ct));
        return filas.Count;
    }
}
