using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Metricas;

public static class clsDependencyInjection
{
    public static IServiceCollection AddMetricasUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsObtenerMetricasRecuperacionQuery, clsMetricasRecuperacionDto>, clsObtenerMetricasRecuperacionQueryHandler>();
        return services;
    }
}
