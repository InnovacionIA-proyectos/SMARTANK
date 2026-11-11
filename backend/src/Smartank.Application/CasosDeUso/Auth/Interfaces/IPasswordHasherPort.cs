namespace Smartank.Application.CasosDeUso.Auth.Interfaces;

/// <summary>Hash y verificacion de contrasenas (bcrypt). Implementado en Infrastructure; nunca recibe ni devuelve la clave en logs.</summary>
public interface IPasswordHasherPort
{
    string Hash(string password);

    /// <summary>True si la contrasena corresponde al hash. Un hash corrupto o de formato desconocido devuelve false, no lanza.</summary>
    bool Verificar(string password, string hash);

    /// <summary>
    /// Hash valido que no corresponde a ninguna cuenta, calculado con el mismo costo que los reales. El login verifica
    /// contra el cuando el correo no existe, para que responder "credenciales invalidas" tarde lo mismo exista o no el correo.
    /// </summary>
    string HashFicticio { get; }
}
