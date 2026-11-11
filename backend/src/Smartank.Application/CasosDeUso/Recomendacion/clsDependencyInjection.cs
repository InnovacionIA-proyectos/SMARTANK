using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Recomendacion.DTOs;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;

namespace Smartank.Application.CasosDeUso.Recomendacion;

public static class clsDependencyInjection
{
    /// <summary>
    /// Requiere <c>AddAutonomiaUseCases</c> (calculo de autonomia) y los repositorios de <c>AddAutorizacionesInfrastructure</c>
    /// y <c>AddRecomendacionInfrastructure</c>.
    /// </summary>
    public static IServiceCollection AddRecomendacionUseCases(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<clsParametrosNegocio>();

        services.AddScoped<ICommandHandler<clsRecomendarTanqueoCommand, clsRecomendacionDto>, clsRecomendarTanqueoCommandHandler>();
        return services;
    }
}
