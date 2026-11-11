using System.Text.RegularExpressions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Auth.Validadores;

public static partial class clsCredencialesValidador
{
    public const int LONGITUD_MAXIMA_EMAIL = 256;

    /// <summary>Bcrypt solo considera los primeros 72 bytes; un tope evita enviar claves enormes al hasher.</summary>
    public const int LONGITUD_MAXIMA_PASSWORD = 200;

    public const int LONGITUD_MINIMA_PASSWORD_ADMIN = 12;

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex PatronEmail();

    /// <summary>Correo sin espacios laterales y en minusculas (la base lo compara sin distinguir mayusculas).</summary>
    public static string NormalizarEmail(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    public static bool EmailValido(string emailNormalizado) =>
        emailNormalizado.Length is > 0 and <= LONGITUD_MAXIMA_EMAIL && PatronEmail().IsMatch(emailNormalizado);

    /// <summary>Valida la forma de las credenciales del login, sin revelar nada sobre la cuenta.</summary>
    public static void ValidarLogin(string emailNormalizado, string? password)
    {
        if (!EmailValido(emailNormalizado))
            throw new clsValidationFailedException("LOGIN_DATOS_INVALIDOS", "Ingrese un correo electrónico válido.");

        if (string.IsNullOrEmpty(password) || password.Length > LONGITUD_MAXIMA_PASSWORD)
            throw new clsValidationFailedException("LOGIN_DATOS_INVALIDOS", "La contraseña es requerida.");
    }

    /// <summary>Correo valido y clave de al menos 12 caracteres: es la cuenta con mas privilegios del sistema.</summary>
    public static void ValidarAdministradorInicial(string emailNormalizado, string? password)
    {
        if (!EmailValido(emailNormalizado))
            throw new clsValidationFailedException(
                "ADMIN_INICIAL_INVALIDO", "Admin__Email no está configurado o no es un correo válido.");

        if (string.IsNullOrEmpty(password) || password.Length < LONGITUD_MINIMA_PASSWORD_ADMIN)
            throw new clsValidationFailedException(
                "ADMIN_INICIAL_INVALIDO",
                $"Admin__Password debe tener al menos {LONGITUD_MINIMA_PASSWORD_ADMIN} caracteres.");

        if (password.Length > LONGITUD_MAXIMA_PASSWORD)
            throw new clsValidationFailedException(
                "ADMIN_INICIAL_INVALIDO",
                $"Admin__Password no puede superar {LONGITUD_MAXIMA_PASSWORD} caracteres.");
    }
}
