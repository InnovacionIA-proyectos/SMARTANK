using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;

namespace Smartank.Application.CasosDeUso.Eventos;

public static class clsDependencyInjection
{
    /// <summary>
    /// Requiere <c>AddEventosInfrastructure</c>, <c>AddAutorizacionesInfrastructure</c> (repositorios) y
    /// <c>AddBitacoraInfrastructure</c> (token firmado). El host debe registrar ILogger (AddLogging) como cualquier app ASP.NET.
    /// </summary>
    public static IServiceCollection AddEventosUseCases(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<clsParametrosNegocio>();

        services.AddScoped<ICommandHandler<clsRegistrarEventoCommand, clsEventoDto>, clsRegistrarEventoCommandHandler>();
        services.AddScoped<ICommandHandler<clsVerificarEventoCommand, clsVerificacionDto>, clsVerificarEventoCommandHandler>();
        services.AddScoped<IQueryHandler<clsListarHistorialTanqueosQuery, clsHistorialTanqueosDto>, clsListarHistorialTanqueosQueryHandler>();
        return services;
    }
}
