using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

public static class clsDependencyInjection
{
    /// <summary>
    /// Registra los casos de uso de sincronizacion y la orquestacion de jobs. Requiere ademas
    /// <c>AddSincronizacionInfrastructure(config)</c> (puertos, opciones <c>Sync</c>) y <c>AddPersistence(config)</c> (auditoria).
    /// </summary>
    public static IServiceCollection AddSincronizacionUseCases(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<clsSincronizarVehiculosCommand, clsResultadoSincronizacion>, clsSincronizarVehiculosCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarConductoresCommand, clsResultadoSincronizacion>, clsSincronizarConductoresCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarViajesCommand, clsResultadoSincronizacion>, clsSincronizarViajesCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarRutasCommand, clsResultadoSincronizacion>, clsSincronizarRutasCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarTanqueosCommand, clsResultadoSincronizacion>, clsSincronizarTanqueosCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarTelemetriaCommand, clsResultadoSincronizacion>, clsSincronizarTelemetriaCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarRendimientoCommand, clsResultadoSincronizacion>, clsSincronizarRendimientoCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarPreciosRutaCommand, clsResultadoSincronizacion>, clsSincronizarPreciosRutaCommandHandler>();
        services.AddScoped<ICommandHandler<clsSincronizarPreciosCommand, clsResultadoPreciosDto>, clsSincronizarPreciosCommandHandler>();
        services.AddScoped<ICommandHandler<clsActualizarPreciosManualCommand, clsResultadoPreciosManualDto>, clsActualizarPreciosManualCommandHandler>();

        // Orquestacion: singleton porque lleva el estado de la agenda (no solapar, una vez por dia); cada job abre su propio scope.
        services.AddSingleton<IEjecutorJob, clsEjecutorJob>();
        services.AddSingleton<clsOrquestadorSincronizacion>();
        return services;
    }
}
