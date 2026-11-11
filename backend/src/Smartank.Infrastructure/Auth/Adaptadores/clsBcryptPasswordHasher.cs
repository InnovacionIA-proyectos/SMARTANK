using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Smartank.Infrastructure.Auth.Adaptadores;

/// <summary>
/// Hash bcrypt (BCrypt.Net-Next). Compatible con los hashes $2a$/$2b$ del sistema anterior: se usa bcrypt "clasico" a
/// proposito, no la variante "enhanced" (que pre-hashea con SHA y no verificaria esos hashes).
/// </summary>
public sealed class clsBcryptPasswordHasher : IPasswordHasherPort
{
    private readonly int _costo;
    private readonly Lazy<string> _hashFicticio;

    public clsBcryptPasswordHasher(IOptions<clsSeguridadOptions> opciones)
    {
        _costo = Math.Clamp(opciones.Value.BcryptRounds, clsSeguridadOptions.COSTO_MINIMO, clsSeguridadOptions.COSTO_MAXIMO);

        // Perezoso: calcularlo cuesta lo mismo que un hash real y solo hace falta si alguien intenta entrar con un correo inexistente.
        _hashFicticio = new Lazy<string>(() => BCryptNet.HashPassword(Guid.NewGuid().ToString("N"), _costo));
    }

    public string HashFicticio => _hashFicticio.Value;

    public string Hash(string password) => BCryptNet.HashPassword(password, _costo);

    public bool Verificar(string password, string hash)
    {
        try
        {
            return BCryptNet.Verify(password, hash);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or BCrypt.Net.SaltParseException)
        {
            // Hash corrupto o con un formato que bcrypt no reconoce: la clave no puede coincidir.
            return false;
        }
    }
}
