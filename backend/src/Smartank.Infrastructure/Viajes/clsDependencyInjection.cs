using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Application.Common.Configuracion;
using Smartank.Infrastructure.Viajes.Repositorios;

namespace Smartank.Infrastructure.Viajes;

public static class clsDependencyInjection
{
    /// <summary>Registra el repositorio de lectura de viajes y liga <see cref="clsParametrosNegocio"/> (rendimiento por defecto) a la seccion ParametrosNegocio.</summary>
    public static IServiceCollection AddViajesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Idempotente: si otro modulo ya lo ligo, el segundo Bind aplica los mismos valores.
        services.AddOptions<clsParametrosNegocio>().Bind(config.GetSection(clsParametrosNegocio.SECCION));
        services.AddScoped<IViajesLecturaRepository, clsEfViajesLecturaRepository>();
        return services;
    }
}
