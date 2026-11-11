using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Telemetria.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Telemetria;

public static class clsDependencyInjection
{
    public static IServiceCollection AddTelemetriaUseCases(this IServiceCollection services)
    {
        // El reloj es inyectable para poder probar el rechazo de lecturas con fecha futura.
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ICommandHandler<clsIngestarTelemetriaCommand, clsIngestaTelemetriaResponse>, clsIngestarTelemetriaCommandHandler>();
        services.AddScoped<IQueryHandler<clsObtenerTelemetriaVehiculoQuery, clsTelemetriaVehiculoDto>, clsObtenerTelemetriaVehiculoQueryHandler>();
        return services;
    }
}
