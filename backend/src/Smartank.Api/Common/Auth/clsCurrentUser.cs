using System.Security.Claims;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Enums;

namespace Smartank.Api.Common.Auth;

/// <summary>Usuario autenticado de la peticion actual, leido de los claims del token de sesion. Sin sesion, todo es null (nunca un usuario "de sistema").</summary>
public sealed class clsCurrentUser(IHttpContextAccessor http) : ICurrentUserPort
{
    private ClaimsPrincipal? Usuario => http.HttpContext?.User;

    public bool EstaAutenticado => Usuario?.Identity?.IsAuthenticated == true;

    public Guid? UsuarioId
    {
        get
        {
            if (!EstaAutenticado) return null;
            // "sub" es el claim del token; NameIdentifier cubre handlers de autenticacion que lo mapean (p. ej. los de prueba).
            var valor = Usuario!.FindFirst("sub")?.Value ?? Usuario.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(valor, out var id) ? id : null;
        }
    }

    public string? Email => EstaAutenticado ? Usuario!.FindFirst("email")?.Value ?? Usuario.FindFirst(ClaimTypes.Email)?.Value : null;

    public Rol? Rol =>
        EstaAutenticado && Enum.TryParse<Rol>(Usuario!.FindFirst(ClaimTypes.Role)?.Value, out var rol) ? rol : null;
}
