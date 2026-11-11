using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Auth;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Infrastructure.Auth.Adaptadores;
using Smartank.Infrastructure.Auth.Repositorios;

namespace Smartank.Infrastructure.Auth;

public static class clsDependencyInjection
{
    /// <summary>
    /// Registra el hasher bcrypt, el emisor de tokens JWT, el repositorio de usuarios y las opciones Jwt, Seguridad y Admin.
    /// No valida los secretos al registrar (la app debe poder arrancar y responder /health sin ellos): el primer login sin
    /// Jwt__Secret falla con un mensaje claro.
    /// </summary>
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<clsJwtOptions>().Bind(config.GetSection(clsJwtOptions.SECCION));
        services.AddOptions<clsSeguridadOptions>().Bind(config.GetSection(clsSeguridadOptions.SECCION));
        services.AddOptions<clsAdminInicialOptions>().Bind(config.GetSection(clsAdminInicialOptions.SECCION));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IPasswordHasherPort, clsBcryptPasswordHasher>();
        services.AddSingleton<ISesionTokenPort, clsJwtSesionTokenAdapter>();
        services.AddScoped<IUsuariosRepository, clsEfUsuariosRepository>();
        return services;
    }
}
