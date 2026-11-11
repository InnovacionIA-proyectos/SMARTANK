using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Smartank.Infrastructure.Auth;

/// <summary>Token de sesion (JWT HS256): variables Jwt__Secret y Jwt__ExpiraHoras.</summary>
public class clsJwtOptions
{
    public const string SECCION = "Jwt";

    /// <summary>HS256 exige una clave de al menos 256 bits; con menos, la firma se rompe por fuerza bruta.</summary>
    public const int LONGITUD_MINIMA_SECRETO = 32;

    public const string EMISOR = "Smartank";
    public const string AUDIENCIA = "Smartank.Api";

    /// <summary>Clave de firma. Secreto: nunca en el repo, solo por variable de entorno.</summary>
    public string? Secret { get; set; }

    /// <summary>Vigencia de la sesion (y de la cookie) en horas.</summary>
    public double ExpiraHoras { get; set; } = 12;

    public TimeSpan Vigencia => TimeSpan.FromHours(ExpiraHoras);

    /// <summary>Texto de error si la configuracion no sirve para firmar tokens, o null si es valida.</summary>
    public string? ErrorDeConfiguracion()
    {
        if (string.IsNullOrWhiteSpace(Secret) || Secret.Length < LONGITUD_MINIMA_SECRETO)
            return $"Jwt__Secret no está configurado o tiene menos de {LONGITUD_MINIMA_SECRETO} caracteres.";
        if (!double.IsFinite(ExpiraHoras) || ExpiraHoras <= 0)
            return "Jwt__ExpiraHoras debe ser un número de horas mayor que cero.";
        return null;
    }

    public SymmetricSecurityKey ClaveDeFirma()
    {
        if (ErrorDeConfiguracion() is { } error) throw new InvalidOperationException(error);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret!));
    }

    /// <summary>Parametros con los que se valida un token de sesion (firma HS256, emisor, audiencia y vigencia).</summary>
    public TokenValidationParameters ParametrosDeValidacion() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = ClaveDeFirma(),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateIssuer = true,
        ValidIssuer = EMISOR,
        ValidateAudience = true,
        ValidAudience = AUDIENCIA,
        ValidateLifetime = true,
        RequireExpirationTime = true,
        RequireSignedTokens = true,
        ClockSkew = TimeSpan.FromMinutes(1),
    };
}
