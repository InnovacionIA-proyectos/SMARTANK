using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Sincroniza el historial real de tanqueos (Distracom y Terpel unificados; upsert por fuente + id de fuente). Enlaza el
/// vehiculo por placa; si no existe en la flota el registro se conserva con VehiculoId null para no subestimar el consumo.
/// <paramref name="DesdeHoraColombia"/> null usa <c>Sync__TanqueosDesde</c>. Idempotente.
/// </summary>
public sealed record clsSincronizarTanqueosCommand(DateTime? DesdeHoraColombia = null) : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarTanqueosCommandHandler(
    ITanqueosLegadoPort legado,
    IVehiculosSincronizacionRepository vehiculos,
    ITanqueosSincronizacionRepository tanqueos,
    IOptions<clsSyncOptions> opciones,
    ILogger<clsSincronizarTanqueosCommandHandler> log)
    : ICommandHandler<clsSincronizarTanqueosCommand, clsResultadoSincronizacion>
{
    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarTanqueosCommand command, CancellationToken ct = default)
    {
        var desde = command.DesdeHoraColombia ?? opciones.Value.TanqueosDesde;

        var tareaDistracom = legado.ListarDistracomAsync(desde, ct);
        var tareaTerpel = legado.ListarTerpelAsync(desde, ct);
        var tareaVehiculos = vehiculos.ObtenerMapaPlacaAIdAsync(ct);
        await Task.WhenAll(tareaDistracom, tareaTerpel, tareaVehiculos);
        var distracom = tareaDistracom.Result;
        var terpel = tareaTerpel.Result;
        var placaAId = tareaVehiculos.Result;

        var sinVehiculo = 0;
        var filas = new List<clsTanqueoUpsertDto>(distracom.Count + terpel.Count);
        foreach (var t in distracom.Concat(terpel))
        {
            Guid? vehiculoId = placaAId.TryGetValue(t.Placa, out var id) ? id : null;
            if (vehiculoId is null) sinVehiculo++;
            filas.Add(new clsTanqueoUpsertDto(t, vehiculoId));
        }

        var resultado = await tanqueos.UpsertAsync(filas, ct);

        log.LogInformation(
            "Sincronizacion de tanqueos desde {Desde:yyyy-MM-dd}: {Total} (distracom {Distracom}, terpel {Terpel}), {Insertados} insertados, {Actualizados} actualizados, {SinVehiculo} sin vehiculo, {Errores} errores",
            desde, filas.Count, distracom.Count, terpel.Count, resultado.Insertados, resultado.Actualizados, sinVehiculo, resultado.Errores.Count);

        return clsResultadoSincronizacion.Crear(
            filas.Count, resultado.Insertados, resultado.Actualizados, 0, resultado.Errores,
            new Dictionary<string, int>
            {
                ["sinVehiculo"] = sinVehiculo,
                ["distracom"] = distracom.Count,
                ["terpel"] = terpel.Count,
            });
    }
}
