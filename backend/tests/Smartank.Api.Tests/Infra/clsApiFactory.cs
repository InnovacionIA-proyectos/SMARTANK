using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Api.Tests.Infra;

/// <summary>
/// Levanta el API REAL (clsProgram, con todo el pipeline: JSON, autenticacion por cookie, roles, rate limiting, errores)
/// en memoria contra la base de <c>TEST_DB_CONNECTION</c>. La configuracion entra por variables de entorno porque
/// clsProgram la lee antes de construir el host. Estas pruebas SI dejan filas (usuarios, viajes, etc.) con marcas
/// aleatorias: usan HTTP real y varias conexiones, asi que no pueden revertirse con una transaccion. Por eso exigen una
/// base de pruebas dedicada, nunca la de produccion.
/// </summary>
public sealed class clsApiFactory : WebApplicationFactory<clsProgram>
{
    public const string CLAVE_PRUEBA = "ClaveDePrueba-2026!";
    public const string URL_PUBLICA = "http://localhost:4200";

    private static readonly string[] _variables =
    [
        "ConnectionStrings__Smartank", "Jwt__Secret", "Bitacora__Secret", "Servicio__Secreto", "Cookie__Secure",
        "App__UrlPublica", "Cors__OrigenesPermitidos",
    ];

    public const string SECRETO_SERVICIO = "secreto-de-servicio-para-pruebas";

    private readonly Dictionary<Rol, HttpClient> _sesiones = new();

    public clsApiFactory()
    {
        // WebApplicationFactory crea el host de forma perezosa; las variables deben estar antes de ese momento.
        Environment.SetEnvironmentVariable("ConnectionStrings__Smartank", clsHechoConBaseDePruebasAttribute.CadenaDeConexion);
        Environment.SetEnvironmentVariable("Jwt__Secret", "secreto-jwt-de-pruebas-con-mas-de-32-caracteres");
        Environment.SetEnvironmentVariable("Bitacora__Secret", "secreto-bitacora-pruebas-con-mas-de-32-caracteres");
        Environment.SetEnvironmentVariable("Servicio__Secreto", SECRETO_SERVICIO);
        Environment.SetEnvironmentVariable("Cookie__Secure", "false"); // el cliente de pruebas usa http://localhost
        Environment.SetEnvironmentVariable("App__UrlPublica", URL_PUBLICA);
        Environment.SetEnvironmentVariable("Cors__OrigenesPermitidos", URL_PUBLICA);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development");

    /// <summary>Cliente SIN sesion (conductor, anonimo).</summary>
    public HttpClient ClienteAnonimo() =>
        CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });

    /// <summary>
    /// Cliente con la sesion de un usuario de ese rol (se crea el usuario y se inicia sesion por la API, una sola vez por
    /// rol: el login tiene rate limit). Con <paramref name="nueva"/> crea una sesion aparte que no se reutiliza (para
    /// pruebas que la destruyen, como el logout).
    /// </summary>
    public async Task<HttpClient> SesionAsync(Rol rol, bool nueva = false)
    {
        if (!nueva && _sesiones.TryGetValue(rol, out var existente)) return existente;

        var email = $"{Snake(rol)}.{Guid.NewGuid():N}@pruebas.smartank.local";
        await using (var db = clsContextoDePruebas.Crear())
        {
            db.Usuarios.Add(new clsUsuario
            {
                Email = email,
                Nombre = $"Usuario {rol}",
                Rol = rol,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(CLAVE_PRUEBA, 4),
            });
            await db.SaveChangesAsync();
        }

        var cliente = CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true, AllowAutoRedirect = false });
        var resp = await cliente.PostAsJsonAsync("/api/auth/login", new { email, password = CLAVE_PRUEBA });
        if (resp.StatusCode != HttpStatusCode.OK)
            throw new InvalidOperationException($"No se pudo iniciar sesion de prueba: {(int)resp.StatusCode} {await resp.Content.ReadAsStringAsync()}");
        if (!nueva) _sesiones[rol] = cliente;
        return cliente;
    }

    public static async Task<JsonElement> LeerJsonAsync(HttpResponseMessage resp)
    {
        var texto = await resp.Content.ReadAsStringAsync();
        return JsonDocument.Parse(texto).RootElement.Clone();
    }

    private static string Snake(Rol rol) => clsSnakeCase.ToSnake(rol);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var c in _sesiones.Values) c.Dispose();
            foreach (var v in _variables) Environment.SetEnvironmentVariable(v, null);
        }
        base.Dispose(disposing);
    }
}
