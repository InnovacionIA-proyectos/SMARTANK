using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Estaciones.Interfaces;
using Smartank.Infrastructure.Estaciones.Repositorios;

namespace Smartank.Infrastructure.Estaciones;

public static class clsDependencyInjection
{
    public static IServiceCollection AddEstacionesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IEstacionesRepository, clsEfEstacionesRepository>();
        return services;
    }
}
