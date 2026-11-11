using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Infrastructure.Alertas.Repositorios;

namespace Smartank.Infrastructure.Alertas;

public static class clsDependencyInjection
{
    public static IServiceCollection AddAlertasInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IAlertasRepository, clsEfAlertasRepository>();
        return services;
    }
}
