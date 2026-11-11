using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Smartank.Api.Common.Middleware;
using Smartank.Infrastructure.Auth;

namespace Smartank.Api.Common.Auth;

/// <summary>
/// Configura JwtBearer: el token se lee SOLO de la cookie de sesion (nunca del header Authorization, para que el unico
/// camino de entrada sea el que protege SameSite=Strict) y los rechazos usan el formato de error estandar del API.
/// </summary>
internal sealed class clsJwtBearerConfigurador(IOptions<clsJwtOptions> jwt, ILogger<clsJwtBearerConfigurador> logger)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(JwtBearerOptions options) => Configure(JwtBearerDefaults.AuthenticationScheme, options);

    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name != JwtBearerDefaults.AuthenticationScheme) return;

        // Los claims quedan con el nombre que trae el token (sub, email, rol), sin renombrarlos a las URIs de WS-Federation.
        options.MapInboundClaims = false;
        options.SaveToken = false;
        options.TokenValidationParameters = ParametrosSeguros();

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Cookies.TryGetValue(clsCookieSesion.NOMBRE, out var token) && !string.IsNullOrEmpty(token))
                    ctx.Token = token;
                else
                    ctx.NoResult();
                return Task.CompletedTask;
            },
            OnChallenge = ctx =>
            {
                ctx.HandleResponse();
                var expirada = ctx.AuthenticateFailure is SecurityTokenExpiredException;
                return clsErrorHandlingMiddleware.WriteAsync(
                    ctx.HttpContext, StatusCodes.Status401Unauthorized,
                    expirada ? "SESION_EXPIRADA" : "NO_AUTENTICADO",
                    expirada ? "La sesión expiró. Inicie sesión de nuevo." : "Se requiere iniciar sesión.");
            },
            OnForbidden = ctx => clsErrorHandlingMiddleware.WriteAsync(
                ctx.HttpContext, StatusCodes.Status403Forbidden,
                "ACCESO_DENEGADO", "Su rol no tiene permiso para esta operación."),
        };
    }

    /// <summary>
    /// Si Jwt__Secret falta o es corto, el servicio NO se cae (los endpoints anonimos como /health deben seguir
    /// respondiendo): se valida contra una clave aleatoria de un solo uso, de modo que ningun token valide (todo lo
    /// protegido responde 401), y se deja un error en el log. El login fallara con un mensaje claro al intentar firmar.
    /// </summary>
    private TokenValidationParameters ParametrosSeguros()
    {
        var error = jwt.Value.ErrorDeConfiguracion();
        if (error is null) return jwt.Value.ParametrosDeValidacion();

        logger.LogError("{Error} Toda ruta protegida respondera 401 hasta que se configure.", error);
        var parametros = new clsJwtOptions { Secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), ExpiraHoras = 1 }
            .ParametrosDeValidacion();
        return parametros;
    }
}
