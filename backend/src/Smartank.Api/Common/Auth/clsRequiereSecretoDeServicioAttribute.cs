using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Api.Common.Auth;

/// <summary>
/// Exige el header <c>X-Service-Secret</c> igual a Servicio__Secreto. Para endpoints maquina a maquina (ingesta de
/// telemetria, creacion de alertas por servicios) que no tienen sesion de usuario: se combina con <c>[AllowAnonymous]</c>.
/// Sin secreto configurado responde 503 SECRETO_SERVICIO_NO_CONFIGURADO (falla cerrado, no abre el endpoint); con un
/// secreto errado, 401 SECRETO_SERVICIO_INVALIDO. Es un filtro de autorizacion: corre antes de leer el cuerpo.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class clsRequiereSecretoDeServicioAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HEADER = "X-Service-Secret";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var esperado = context.HttpContext.RequestServices.GetRequiredService<IOptionsMonitor<clsServicioOptions>>().CurrentValue.Secreto;
        if (string.IsNullOrWhiteSpace(esperado))
            throw new clsServiceUnavailableException(
                "SECRETO_SERVICIO_NO_CONFIGURADO", "El secreto de servicio no está configurado en el servidor.");

        var enviado = context.HttpContext.Request.Headers[HEADER].ToString();
        if (!Coincide(esperado, enviado))
            throw new clsUnauthorizedException("SECRETO_SERVICIO_INVALIDO", "Secreto de servicio inválido.");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Comparacion en tiempo constante: <see cref="CryptographicOperations.FixedTimeEquals"/> sobre los hashes SHA-256 de ambos
    /// valores. Se comparan los hashes (siempre de 32 bytes) para que ni el contenido ni la LONGITUD del secreto se deduzcan
    /// midiendo el tiempo de respuesta.
    /// </summary>
    internal static bool Coincide(string esperado, string? enviado)
    {
        if (string.IsNullOrEmpty(enviado)) return false;
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(esperado));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(enviado));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
