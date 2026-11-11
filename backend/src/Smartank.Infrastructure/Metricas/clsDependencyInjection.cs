using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Metricas.Interfaces;
using Smartank.Infrastructure.Metricas.Repositorios;

namespace Smartank.Infrastructure.Metricas;

public static class clsDependencyInjection
{
    public static IServiceCollection AddMetricasInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IMetricasRepository, clsSqlMetricasRepository>();
        return services;
    }
}
