using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

/// <summary>Resuelve el caso de uso de cada job en un scope propio (los handlers son scoped) y lo ejecuta.</summary>
public sealed class clsEjecutorJob(IServiceScopeFactory scopes) : IEjecutorJob
{
    public async Task<clsResultadoSincronizacion> EjecutarAsync(string nombreJob, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        return nombreJob switch
        {
            clsNombresJob.VEHICULOS => await Ejecutar<clsSincronizarVehiculosCommand>(sp, new(), ct),
            clsNombresJob.CONDUCTORES => await Ejecutar<clsSincronizarConductoresCommand>(sp, new(), ct),
            clsNombresJob.VIAJES => await Ejecutar<clsSincronizarViajesCommand>(sp, new(), ct),
            clsNombresJob.RUTAS => await Ejecutar<clsSincronizarRutasCommand>(sp, new(Incremental: false), ct),
            clsNombresJob.RUTAS_INCREMENTAL => await Ejecutar<clsSincronizarRutasCommand>(sp, new(Incremental: true), ct),
            clsNombresJob.TANQUEOS => await Ejecutar<clsSincronizarTanqueosCommand>(sp, new(), ct),
            clsNombresJob.TELEMETRIA => await Ejecutar<clsSincronizarTelemetriaCommand>(sp, new(), ct),
            clsNombresJob.PRECIOS => (await sp
                .GetRequiredService<ICommandHandler<clsSincronizarPreciosCommand, clsResultadoPreciosDto>>()
                .Handle(new clsSincronizarPreciosCommand(), ct)).ComoResumen(),
            clsNombresJob.PRECIOS_RUTA => await Ejecutar<clsSincronizarPreciosRutaCommand>(sp, new(), ct),
            clsNombresJob.RENDIMIENTO => await Ejecutar<clsSincronizarRendimientoCommand>(sp, new(), ct),
            _ => throw new ArgumentException($"'{nombreJob}' no es un job de sincronizacion individual.", nameof(nombreJob)),
        };
    }

    private static Task<clsResultadoSincronizacion> Ejecutar<TCommand>(IServiceProvider sp, TCommand command, CancellationToken ct)
        where TCommand : ICommand<clsResultadoSincronizacion> =>
        sp.GetRequiredService<ICommandHandler<TCommand, clsResultadoSincronizacion>>().Handle(command, ct);
}
