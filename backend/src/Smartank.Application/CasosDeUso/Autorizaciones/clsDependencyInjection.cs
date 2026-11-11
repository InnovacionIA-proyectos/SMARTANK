using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;

namespace Smartank.Application.CasosDeUso.Autorizaciones;

public static class clsDependencyInjection
{
    public static IServiceCollection AddAutorizacionesUseCases(this IServiceCollection services)
    {
        // Los handlers leen la hora y los parametros de negocio: se garantiza que existan aunque el host no los registre.
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<clsParametrosNegocio>();
        services.AddOptions<clsAplicacionOptions>();

        services.AddScoped<ICommandHandler<clsCrearAutorizacionCommand, clsAutorizacionDto>, clsCrearAutorizacionCommandHandler>();
        services.AddScoped<ICommandHandler<clsAutorizarAutorizacionCommand, clsAutorizacionDto>, clsAutorizarAutorizacionCommandHandler>();
        services.AddScoped<ICommandHandler<clsPreautorizarEdsCommand, clsPreautorizacionEdsDto>, clsPreautorizarEdsCommandHandler>();
        services.AddScoped<ICommandHandler<clsNotificarAutorizacionCommand, clsNotificarDto>, clsNotificarAutorizacionCommandHandler>();
        services.AddScoped<IQueryHandler<clsObtenerAutorizacionDetalleQuery, clsAutorizacionDetalleDto>, clsObtenerAutorizacionDetalleQueryHandler>();
        services.AddScoped<IQueryHandler<clsListarAutorizacionesQuery, clsAutorizacionesListaDto>, clsListarAutorizacionesQueryHandler>();
        return services;
    }
}
