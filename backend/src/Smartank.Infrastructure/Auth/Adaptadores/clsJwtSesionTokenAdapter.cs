using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Domain.Enums;

namespace Smartank.Infrastructure.Auth.Adaptadores;

/// <summary>
/// Emite y valida el JWT de sesion (HS256). Claims: <c>sub</c> (id), <c>email</c>, <c>nombre</c> y el rol como
/// <see cref="ClaimTypes.Role"/> con el nombre del enum (Despacho, ControlFlota, Finanzas, Admin), que es el valor que
/// usa <c>[Authorize(Roles = ...)]</c>.
/// </summary>
public sealed class clsJwtSesionTokenAdapter(IOptions<clsJwtOptions> opciones, TimeProvider reloj) : ISesionTokenPort
{
    private static readonly JsonWebTokenHandler MANEJADOR = new() { MapInboundClaims = false };

    public clsSesionEmitida Emitir(clsUsuarioDto usuario)
    {
        var config = opciones.Value;
        var ahora = reloj.GetUtcNow();
        var expira = ahora + config.Vigencia;

        var claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = usuario.Id.ToString(),
            [JwtRegisteredClaimNames.Email] = usuario.Email,
            [ClaimTypes.Role] = usuario.Rol.ToString(),
        };
        if (!string.IsNullOrWhiteSpace(usuario.Nombre)) claims["nombre"] = usuario.Nombre;

        var token = MANEJADOR.CreateToken(new SecurityTokenDescriptor
        {
            Claims = claims,
            Issuer = clsJwtOptions.EMISOR,
            Audience = clsJwtOptions.AUDIENCIA,
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = new SigningCredentials(config.ClaveDeFirma(), SecurityAlgorithms.HmacSha256),
        });

        return new clsSesionEmitida(token, expira);
    }

    public clsSesionClaims? Validar(string token)
    {
        var resultado = MANEJADOR.ValidateTokenAsync(token, opciones.Value.ParametrosDeValidacion()).GetAwaiter().GetResult();
        if (!resultado.IsValid) return null;

        var identidad = resultado.ClaimsIdentity;
        var sub = identidad.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var email = identidad.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var rol = identidad.FindFirst(ClaimTypes.Role)?.Value;

        return Guid.TryParse(sub, out var id) && email is not null && Enum.TryParse<Rol>(rol, out var r)
            ? new clsSesionClaims(id, email, r)
            : null;
    }
}
