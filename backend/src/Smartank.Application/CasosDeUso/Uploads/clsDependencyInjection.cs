using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;
using Smartank.Application.CasosDeUso.Uploads.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Uploads;

public static class clsDependencyInjection
{
    /// <summary>Requiere <c>AddUploadsInfrastructure</c>, <c>AddAutorizacionesInfrastructure</c> y <c>AddBitacoraInfrastructure</c>.</summary>
    public static IServiceCollection AddUploadsUseCases(this IServiceCollection services)
    {
        services.AddOptions<clsAplicacionOptions>();

        services.AddScoped<ICommandHandler<clsSubirImagenCommand, clsImagenSubidaDto>, clsSubirImagenCommandHandler>();
        services.AddScoped<IQueryHandler<clsObtenerImagenQuery, clsImagenDto>, clsObtenerImagenQueryHandler>();
        return services;
    }
}
