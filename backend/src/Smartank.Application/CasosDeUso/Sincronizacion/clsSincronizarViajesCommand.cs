using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Sincroniza los fletes (FOs) del legado como viajes (upsert por FoId). El estado se deriva (borrado, cerrado por SAP,
/// despacho vencido o futuro), el manifiesto es el numero de FO sin ceros y se omiten los FO cuyo cabezote no existe en la flota.
/// <paramref name="DesdeHoraColombia"/> null usa <c>Sync__FosDesde</c>. Idempotente.
/// </summary>
public sealed record clsSincronizarViajesCommand(DateTime? DesdeHoraColombia = null) : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarViajesCommandHandler(
    IFletesLegadoPort fletes,
    IManifiestosCerradosPort manifiestosCerrados,
    IVehiculosSincronizacionRepository vehiculos,
    IViajesSincronizacionRepository viajes,
    IOptions<clsSyncOptions> opciones,
    TimeProvider reloj,
    ILogger<clsSincronizarViajesCommandHandler> log)
    : ICommandHandler<clsSincronizarViajesCommand, clsResultadoSincronizacion>
{
    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarViajesCommand command, CancellationToken ct = default)
    {
        var desde = command.DesdeHoraColombia ?? opciones.Value.FosDesde;

        var tareaFletes = fletes.ListarDesdeAsync(desde, ct);
        var tareaCerrados = manifiestosCerrados.ListarDesdeAsync(desde, ct);
        var tareaVehiculos = vehiculos.ObtenerMapaPlacaAIdAsync(ct);
        await Task.WhenAll(tareaFletes, tareaCerrados, tareaVehiculos);
        var lista = tareaFletes.Result;
        var cerrados = tareaCerrados.Result;
        var placaAId = tareaVehiculos.Result;

        // Una sola marca de tiempo para clasificar en_ruta/programado de forma consistente en toda la corrida.
        var ahora = reloj.GetUtcNow();

        var porFo = new Dictionary<long, clsViajeUpsertDto>();
        var omitidos = 0;
        foreach (var f in lista)
        {
            if (!placaAId.TryGetValue(f.PlacaCabezote.Trim(), out var vehiculoId))
            {
                omitidos++;
                continue;
            }

            var manifiesto = clsReglasViaje.Manifiesto(f.NumeroFo);
            var cerradoPorSap = manifiesto is not null && cerrados.Contains(manifiesto);
            var estado = clsEstadoFlete.Derivar(f.Borrado, f.FechaDespacho, cerradoPorSap, ahora);

            porFo[f.FoId] = new clsViajeUpsertDto(
                f.FoId, vehiculoId, clsReglasViaje.ResolverRuta(f.NombreRuta, f.CodigoRuta), estado, manifiesto,
                clsReglasViaje.InicioAt(estado, f.FechaDespacho),
                f.PlacaCabezote.Trim(), f.PlacaTrailer, f.ProgramaCabezote, f.CedulaConductor, f.Transportista,
                manifiesto, f.CodigoRuta, f.NombreRuta, f.CiudadOrigen, f.CiudadDestino,
                f.PesoFo, f.GalonesEds, f.GalonesCredito, f.GalonesContado, f.GalonesRuta, f.KmtsRuta, f.FechaDespacho,
                f.GastosGenerados, f.GastosEnviadoSap, f.ConsumoEnviadoSap, f.EnviadoSap, f.Borrado);
        }

        var resultado = await viajes.UpsertAsync(porFo.Values.ToList(), ct);

        log.LogInformation(
            "Sincronizacion de viajes desde {Desde:yyyy-MM-dd}: {Total} fletes, {Cerrados} manifiestos cerrados en SAP, {Insertados} insertados, {Actualizados} actualizados, {Omitidos} omitidos sin vehiculo, {Errores} errores",
            desde, lista.Count, cerrados.Count, resultado.Insertados, resultado.Actualizados, omitidos, resultado.Errores.Count);

        return clsResultadoSincronizacion.Crear(
            lista.Count, resultado.Insertados, resultado.Actualizados, omitidos, resultado.Errores,
            new Dictionary<string, int> { ["manifiestosCerrados"] = cerrados.Count });
    }
}
