namespace Smartank.Api.Common.Auth;

/// <summary>
/// Cookie que lleva el JWT de sesion. HttpOnly (el JavaScript de la SPA no puede leerla, lo que limita el robo por XSS) y
/// SameSite=Strict (el navegador no la envia en peticiones iniciadas desde otro sitio, que es la defensa contra CSRF).
/// </summary>
public static class clsCookieSesion
{
    public const string NOMBRE = "smartank_session";

    public static void Escribir(HttpResponse respuesta, string token, DateTimeOffset expiraAt, bool secure) =>
        respuesta.Cookies.Append(NOMBRE, token, Opciones(secure, MaxAgeHasta(expiraAt)));

    /// <summary>Borra la cookie. Debe usar los mismos Path, SameSite y Secure con que se creo o el navegador no la reconoce como la misma.</summary>
    public static void Borrar(HttpResponse respuesta, bool secure) =>
        respuesta.Cookies.Delete(NOMBRE, Opciones(secure, null));

    private static TimeSpan MaxAgeHasta(DateTimeOffset expiraAt)
    {
        var restante = expiraAt - DateTimeOffset.UtcNow;
        return restante > TimeSpan.Zero ? restante : TimeSpan.Zero;
    }

    private static CookieOptions Opciones(bool secure, TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = secure,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        MaxAge = maxAge,
        IsEssential = true,
    };
}
