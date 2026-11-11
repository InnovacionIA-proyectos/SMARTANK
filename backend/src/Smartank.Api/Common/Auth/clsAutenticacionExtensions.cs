using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Smartank.Application.Common.Ports;
using Smartank.Infrastructure.Auth;

namespace Smartank.Api.Common.Auth;

/// <summary>Registro de la autenticacion y autorizacion de SMARTANK (cookie httpOnly con JWT, roles, politica por defecto y secreto de servicio).</summary>
public static class clsAutenticacionExtensions
{
    /// <summary>
    /// Registra: el esquema JwtBearer que lee la cookie <c>smartank_session</c>, las opciones Jwt, Cookie y Servicio, el
    /// usuario actual (<see cref="ICurrentUserPort"/>) y la politica por defecto (FallbackPolicy): todo endpoint exige
    /// usuario autenticado salvo que declare <c>[AllowAnonymous]</c>. Los roles se exigen con <c>[Authorize(Roles = clsRoles.X)]</c>.
    /// En el pipeline hay que llamar a UseAuthentication y UseAuthorization (en ese orden, despues del ruteo); los
    /// endpoints que no son controllers (por ejemplo /health) deben marcarse con AllowAnonymous().
    /// </summary>
    public static IServiceCollection AddAutenticacionSmartank(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<clsJwtOptions>().Bind(config.GetSection(clsJwtOptions.SECCION));
        services.AddOptions<clsCookieOptions>().Bind(config.GetSection(clsCookieOptions.SECCION));
        services.AddOptions<clsServicioOptions>().Bind(config.GetSection(clsServicioOptions.SECCION));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserPort, clsCurrentUser>();

        services.AddSingleton<IConfigureOptions<JwtBearerOptions>, clsJwtBearerConfigurador>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }

    /// <summary>
    /// Falla el arranque (InvalidOperationException) si Jwt__Secret falta, mide menos de 32 caracteres o la vigencia no es
    /// valida. Llamarlo al arrancar en Produccion: sin el, el servicio levanta pero nadie puede iniciar sesion.
    /// </summary>
    public static void ValidarConfiguracionAutenticacion(this IConfiguration config)
    {
        var jwt = config.GetSection(clsJwtOptions.SECCION).Get<clsJwtOptions>() ?? new clsJwtOptions();
        if (jwt.ErrorDeConfiguracion() is { } error)
            throw new InvalidOperationException(error);
    }
}
