using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Estaciones.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Estaciones;

public static class clsDependencyInjection
{
    public static IServiceCollection AddEstacionesUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsListarEstacionesQuery, clsEstacionesResponse>, clsListarEstacionesQueryHandler>();
        return services;
    }
}
