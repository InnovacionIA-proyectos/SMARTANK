using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Viajes.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Viajes;

public static class clsDependencyInjection
{
    public static IServiceCollection AddViajesUseCases(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IQueryHandler<clsListarViajesQuery, clsViajesResponse>, clsListarViajesQueryHandler>();
        services.AddScoped<IQueryHandler<clsObtenerRutaViajeQuery, clsRutaViajeDto>, clsObtenerRutaViajeQueryHandler>();
        return services;
    }
}
