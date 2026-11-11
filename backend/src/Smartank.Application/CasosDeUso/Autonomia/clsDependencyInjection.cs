using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Autonomia.DTOs;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;

namespace Smartank.Application.CasosDeUso.Autonomia;

public static class clsDependencyInjection
{
    /// <summary>Requiere <c>IContextoTanqueoRepository</c> (lo registra <c>AddAutorizacionesInfrastructure</c>).</summary>
    public static IServiceCollection AddAutonomiaUseCases(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<clsParametrosNegocio>();

        services.AddScoped<ICommandHandler<clsCalcularAutonomiaCommand, clsAutonomiaDto>, clsCalcularAutonomiaCommandHandler>();
        return services;
    }
}
