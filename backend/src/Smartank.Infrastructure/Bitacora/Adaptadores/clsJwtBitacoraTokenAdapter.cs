using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Infrastructure.Bitacora.Adaptadores;

/// <summary>
/// Token JWT HS256 de la bitacora. Claims: <c>autorizacion_id</c> y <c>proposito = bitacora</c>. El proposito impide que un token
/// de otro uso (por ejemplo, el de la sesion, aunque compartiera secreto) sirva como enlace de bitacora.
/// </summary>
public sealed class clsJwtBitacoraTokenAdapter(IOptions<clsBitacoraOptions> options, TimeProvider reloj) : IBitacoraTokenPort
{
    private const string CLAIM_AUTORIZACION = "autorizacion_id";
    private const string CLAIM_PROPOSITO = "proposito";
    private const string PROPOSITO_BITACORA = "bitacora";
    private static readonly TimeSpan DESFASE_RELOJ = TimeSpan.FromSeconds(30);

    public string Emitir(Guid autorizacionId)
    {
        var o = options.Value;
        var ahora = reloj.GetUtcNow().UtcDateTime;
        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(CLAIM_AUTORIZACION, autorizacionId.ToString()),
                new Claim(CLAIM_PROPOSITO, PROPOSITO_BITACORA),
            ],
            notBefore: ahora,
            expires: ahora.AddHours(o.ExpiraHoras),
            signingCredentials: new SigningCredentials(Llave(o), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid Validar(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) throw Invalido();

        var parametros = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = Llave(options.Value),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateLifetime = true,
            ClockSkew = DESFASE_RELOJ,
            // La vigencia se juzga con el reloj inyectado (no con el del sistema) para poder probarla.
            LifetimeValidator = ValidarVigencia,
        };

        ClaimsPrincipal principal;
        try
        {
            principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(token, parametros, out _);
        }
        catch (SecurityTokenExpiredException)
        {
            throw Expirado();
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            throw Invalido();
        }

        if (principal.FindFirst(CLAIM_PROPOSITO)?.Value != PROPOSITO_BITACORA
            || !Guid.TryParse(principal.FindFirst(CLAIM_AUTORIZACION)?.Value, out var autorizacionId)
            || autorizacionId == Guid.Empty)
            throw Invalido();

        return autorizacionId;
    }

    private bool ValidarVigencia(DateTime? notBefore, DateTime? expires, SecurityToken? token, TokenValidationParameters parametros)
    {
        var ahora = reloj.GetUtcNow().UtcDateTime;
        if (expires is null) return false;
        if (ahora > expires.Value.ToUniversalTime() + DESFASE_RELOJ) throw new SecurityTokenExpiredException("Token vencido.");
        return notBefore is null || ahora >= notBefore.Value.ToUniversalTime() - DESFASE_RELOJ;
    }

    /// <summary>El secreto es obligatorio; sin el no se puede ni emitir ni validar (503, no 401: es un fallo de configuracion).</summary>
    private static SymmetricSecurityKey Llave(clsBitacoraOptions o)
    {
        if (!o.SecretoValido)
            throw new clsServiceUnavailableException(
                "BITACORA_SECRET_NO_CONFIGURADO",
                $"Falta el secreto de la bitácora (Bitacora__Secret, mínimo {clsBitacoraOptions.LONGITUD_MINIMA_SECRETO} caracteres).");
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(o.Secret!));
    }

    private static clsUnauthorizedException Invalido() =>
        new("BITACORA_TOKEN_INVALIDO", "El enlace de la bitácora no es válido.");

    private static clsUnauthorizedException Expirado() =>
        new("BITACORA_TOKEN_EXPIRADO", "El enlace de la bitácora expiró. Solicite una nueva orden de tanqueo.");
}
