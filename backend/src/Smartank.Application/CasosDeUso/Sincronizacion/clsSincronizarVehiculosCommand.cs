using Microsoft.Extensions.Logging;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>Sincroniza la flota: maestro de cabezotes ACTIVOS del legado a la tabla de vehiculos (upsert por placa). Idempotente.</summary>
public sealed record clsSincronizarVehiculosCommand : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarVehiculosCommandHandler(
    ICabezotesLegadoPort legado,
    IVehiculosSincronizacionRepository vehiculos,
    ILogger<clsSincronizarVehiculosCommandHandler> log)
    : ICommandHandler<clsSincronizarVehiculosCommand, clsResultadoSincronizacion>
{
    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarVehiculosCommand command, CancellationToken ct = default)
    {
        var cabezotes = await legado.ListarActivosAsync(ct);

        // Sin placa no hay llave de upsert: se omite en vez de fallar la corrida.
        var validos = cabezotes.Where(c => !string.IsNullOrWhiteSpace(c.Placa)).ToList();
        var resultado = await vehiculos.UpsertDesdeLegadoAsync(validos, ct);

        log.LogInformation("Sincronizacion de vehiculos: {Total} leidos, {Insertados} insertados, {Actualizados} actualizados, {Errores} errores",
            cabezotes.Count, resultado.Insertados, resultado.Actualizados, resultado.Errores.Count);

        return clsResultadoSincronizacion.Crear(
            cabezotes.Count, resultado.Insertados, resultado.Actualizados, cabezotes.Count - validos.Count, resultado.Errores);
    }
}
