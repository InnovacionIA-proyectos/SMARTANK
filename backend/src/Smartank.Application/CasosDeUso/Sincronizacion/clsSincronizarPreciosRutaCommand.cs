using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Precalcula el precio de ACPM de referencia por viaje para el panel de margen: por cada viaje con ruta halla las EDS de
/// ACPM a menos de <c>Sync__RutaEdsUmbralKm</c> de su polilinea y guarda el precio minimo, el maximo y cuantas EDS son.
/// Depende de rutas y precios, por eso el diario lo corre despues de ambos. Idempotente.
/// </summary>
public sealed record clsSincronizarPreciosRutaCommand : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarPreciosRutaCommandHandler(
    IEstacionesSincronizacionRepository estaciones,
    IPreciosRutaRepository repositorio,
    IOptions<clsSyncOptions> opciones,
    ILogger<clsSincronizarPreciosRutaCommandHandler> log)
    : ICommandHandler<clsSincronizarPreciosRutaCommand, clsResultadoSincronizacion>
{
    private const int TAMANO_LOTE = 500;

    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarPreciosRutaCommand command, CancellationToken ct = default)
    {
        var umbralKm = opciones.Value.UmbralEdsEfectivoKm;
        var eds = await estaciones.ListarConPrecioAcpmAsync(ct);

        var lote = new List<clsPrecioReferenciaViajeDto>(TAMANO_LOTE);
        var procesados = 0;
        var conEds = 0;
        var insertados = 0;
        var actualizados = 0;
        var errores = new List<clsErrorSincronizacion>();

        async Task VaciarLoteAsync()
        {
            if (lote.Count == 0) return;
            var r = await repositorio.UpsertAsync(lote.ToList(), ct);
            insertados += r.Insertados;
            actualizados += r.Actualizados;
            errores.AddRange(r.Errores);
            lote.Clear();
        }

        await foreach (var ruta in repositorio.ListarRutasConPolilineaAsync(ct))
        {
            var referencia = clsSeleccionEds.Calcular(ruta.Polilinea, eds, umbralKm);
            procesados++;
            if (referencia.EdsCerca > 0) conEds++;
            lote.Add(new clsPrecioReferenciaViajeDto(ruta.ViajeId, referencia.AcpmMin, referencia.AcpmMax, referencia.EdsCerca));
            if (lote.Count >= TAMANO_LOTE) await VaciarLoteAsync();
        }
        await VaciarLoteAsync();

        log.LogInformation(
            "Precios por ruta: {Procesados} viajes procesados, {ConEds} con EDS de ACPM a menos de {Umbral} km ({Errores} errores; {Estaciones} EDS candidatas)",
            procesados, conEds, umbralKm, errores.Count, eds.Count);

        return clsResultadoSincronizacion.Crear(
            procesados, insertados, actualizados, 0, errores,
            new Dictionary<string, int> { ["conEdsCerca"] = conEds, ["edsCandidatas"] = eds.Count });
    }
}
