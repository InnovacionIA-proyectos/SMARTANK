using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Auth;

public static class clsDependencyInjection
{
    public static IServiceCollection AddAuthUseCases(this IServiceCollection services)
    {
        services.AddScoped<ICommandHandler<clsLoginCommand, clsLoginResultado>, clsLoginCommandHandler>();
        services.AddScoped<IQueryHandler<clsObtenerSesionActualQuery, clsUsuarioDto>, clsObtenerSesionActualQueryHandler>();
        services.AddScoped<ICommandHandler<clsCrearAdministradorInicialCommand, clsCrearAdministradorResultado>, clsCrearAdministradorInicialCommandHandler>();
        return services;
    }
}
