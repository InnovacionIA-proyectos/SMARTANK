namespace Smartank.Api.Common.Auth;

/// <summary>Cookie de sesion: variable Cookie__Secure.</summary>
public class clsCookieOptions
{
    public const string SECCION = "Cookie";

    /// <summary>
    /// Marca la cookie como Secure (solo viaja por HTTPS). Es true por defecto; ponerla en false solo se justifica al servir
    /// por HTTP plano en un entorno de pruebas, porque el navegador descarta una cookie Secure recibida por HTTP y el login
    /// "no entra" sin mostrar error.
    /// </summary>
    public bool Secure { get; set; } = true;
}
