using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Bitacora.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Bitacora;

public static class clsDependencyInjection
{
    /// <summary>Requiere <c>AddBitacoraInfrastructure</c> (token firmado y consulta).</summary>
    public static IServiceCollection AddBitacoraUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsObtenerBitacoraQuery, clsBitacoraDto>, clsObtenerBitacoraQueryHandler>();
        return services;
    }
}
