using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Telemetria.Interfaces;
using Smartank.Infrastructure.Telemetria.Repositorios;

namespace Smartank.Infrastructure.Telemetria;

public static class clsDependencyInjection
{
    public static IServiceCollection AddTelemetriaInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<ITelemetriaRepository, clsEfTelemetriaRepository>();
        return services;
    }
}
