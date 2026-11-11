using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Infrastructure.Sincronizacion.Adaptadores.Geotab;

/// <summary>
/// Cliente de solo lectura de MyGeotab (JSON-RPC sobre HTTP: <c>POST https://&lt;servidor&gt;/apiv1</c>). Autentica una vez y
/// reutiliza la sesion (cache singleton con semaforo), sigue la federacion de servidores (<c>path</c> distinto de
/// "ThisServer") y, ante un error de la API, reautentica UNA vez y reintenta. Reintentos y timeout de red los pone el handler de
/// resiliencia registrado en <c>AddSincronizacionInfrastructure</c>.
/// </summary>
public sealed class clsGeotabClient(
    HttpClient http,
    clsGeotabSesionCache cache,
    IOptions<clsGeotabOptions> opciones,
    ILogger<clsGeotabClient> log) : IGeotabPort
{
    private const string SERVIDOR_THIS = "ThisServer";
    private const string DIAGNOSTICO_NIVEL = "DiagnosticFuelLevelId";
    private const string DIAGNOSTICO_COMBUSTIBLE_USADO = "DiagnosticDeviceTotalFuelId";
    private const string DIAGNOSTICO_ODOMETRO = "DiagnosticOdometerId";

    private static readonly JsonSerializerOptions JsonOpciones = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<clsGeotabDispositivoDto>> ListarDispositivosAsync(CancellationToken ct = default)
    {
        var resultado = await GetAsync("Device", null, ct);
        var lista = new List<clsGeotabDispositivoDto>();
        foreach (var d in Elementos(resultado))
            if (Texto(d, "id") is { } id) lista.Add(new clsGeotabDispositivoDto(id, Texto(d, "name"), Texto(d, "licensePlate")));
        return lista;
    }

    public async Task<IReadOnlyList<clsGeotabEstadoDto>> ObtenerEstadoDispositivosAsync(CancellationToken ct = default)
    {
        var resultado = await GetAsync("DeviceStatusInfo", null, ct);
        var lista = new List<clsGeotabEstadoDto>();
        foreach (var s in Elementos(resultado))
            if (IdDispositivo(s) is { } id)
                lista.Add(new clsGeotabEstadoDto(id, Numero(s, "latitude"), Numero(s, "longitude"), Fecha(s, "dateTime")));
        return lista;
    }

    public Task<IReadOnlyList<clsGeotabLecturaDto>> ObtenerNivelCombustibleAsync(DateTimeOffset desde, CancellationToken ct = default) =>
        LeerStatusDataAsync(DIAGNOSTICO_NIVEL, desde, ct);

    public Task<IReadOnlyList<clsGeotabLecturaDto>> ObtenerCombustibleUsadoAsync(DateTimeOffset desde, CancellationToken ct = default) =>
        LeerStatusDataAsync(DIAGNOSTICO_COMBUSTIBLE_USADO, desde, ct);

    public Task<IReadOnlyList<clsGeotabLecturaDto>> ObtenerOdometroAsync(DateTimeOffset desde, CancellationToken ct = default) =>
        LeerStatusDataAsync(DIAGNOSTICO_ODOMETRO, desde, ct);

    private async Task<IReadOnlyList<clsGeotabLecturaDto>> LeerStatusDataAsync(string diagnostico, DateTimeOffset desde, CancellationToken ct)
    {
        var busqueda = new
        {
            diagnosticSearch = new { id = diagnostico },
            fromDate = desde.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture),
        };
        var resultado = await GetAsync("StatusData", busqueda, ct);
        var lista = new List<clsGeotabLecturaDto>();
        foreach (var l in Elementos(resultado))
        {
            // Se descartan lecturas incompletas: sin dispositivo, valor numerico o fecha no hay nada que comparar.
            if (IdDispositivo(l) is { } id && Numero(l, "data") is { } valor && Fecha(l, "dateTime") is { } fecha)
                lista.Add(new clsGeotabLecturaDto(id, valor, fecha));
        }
        return lista;
    }

    /// <summary><c>Get</c> tipado. Si la API responde con error se asume sesion vencida: se reautentica una vez y se reintenta.</summary>
    private async Task<JsonElement> GetAsync(string typeName, object? busqueda, CancellationToken ct)
    {
        var o = ValidarConfiguracion();
        var sesion = await cache.ObtenerAsync(t => AutenticarAsync(o, t), null, ct);
        try
        {
            return await LlamarGetAsync(sesion, typeName, busqueda, ct);
        }
        catch (clsGeotabApiException ex)
        {
            log.LogInformation("Geotab rechazo la consulta ({Mensaje}); se reautentica una vez", ex.Message);
            var renovada = await cache.ObtenerAsync(t => AutenticarAsync(o, t), sesion, ct);
            return await LlamarGetAsync(renovada, typeName, busqueda, ct);
        }
    }

    private Task<JsonElement> LlamarGetAsync(clsGeotabSesion sesion, string typeName, object? busqueda, CancellationToken ct)
    {
        var credenciales = new { database = sesion.Database, userName = sesion.UserName, sessionId = sesion.SessionId };
        object parametros = busqueda is null
            ? new { typeName, credentials = credenciales }
            : new { typeName, credentials = credenciales, search = busqueda };
        return RpcAsync(sesion.Servidor, "Get", parametros, ct);
    }

    private async Task<clsGeotabSesion> AutenticarAsync(clsGeotabOptions o, CancellationToken ct)
    {
        var servidor = string.IsNullOrWhiteSpace(o.Server) ? clsGeotabOptions.SERVIDOR_POR_DEFECTO : o.Server.Trim();
        var resultado = await RpcAsync(servidor, "Authenticate", new { database = o.Database, userName = o.Username, password = o.Password }, ct);

        if (!resultado.TryGetProperty("credentials", out var credenciales) || Texto(credenciales, "sessionId") is not { } sessionId)
            throw new clsGeotabApiException("Geotab no devolvio credenciales de sesion.");

        // "path" distinto de ThisServer es el host donde vive la base de datos (federacion de servidores).
        var path = Texto(resultado, "path");
        var efectivo = path is not null && !string.Equals(path, SERVIDOR_THIS, StringComparison.OrdinalIgnoreCase) ? path : servidor;

        log.LogInformation("Sesion Geotab establecida en {Servidor} (base {Database})", efectivo, o.Database);
        return new clsGeotabSesion(
            Texto(credenciales, "database") ?? o.Database!, Texto(credenciales, "userName") ?? o.Username!, sessionId, efectivo);
    }

    /// <summary>Llamada JSON-RPC cruda a un servidor Geotab; devuelve <c>result</c> (copia independiente del documento).</summary>
    private async Task<JsonElement> RpcAsync(string servidor, string metodo, object parametros, CancellationToken ct)
    {
        var url = $"{BaseUrl(servidor)}/apiv1";
        HttpResponseMessage? recibida = null;
        try
        {
            recibida = await http.PostAsJsonAsync(url, new { method = metodo, @params = parametros }, JsonOpciones, ct);
            recibida.EnsureSuccessStatusCode();
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            recibida?.Dispose();
            throw new clsServiceUnavailableException(
                "GEOTAB_NO_DISPONIBLE", $"No se pudo conectar a Geotab ({servidor}): {ex.Message}");
        }

        using (recibida)
        {
            JsonDocument doc;
            try
            {
                doc = await JsonDocument.ParseAsync(await recibida!.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            }
            catch (JsonException)
            {
                throw new clsGeotabApiException($"Geotab respondio algo que no es JSON en {metodo}.");
            }

            using (doc)
            {
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("error", out var error)
                    && error.ValueKind != JsonValueKind.Null)
                {
                    throw new clsGeotabApiException(
                        $"{metodo}: {Texto(error, "message") ?? error.GetRawText()}");
                }
                return doc.RootElement.TryGetProperty("result", out var resultado) ? resultado.Clone() : default;
            }
        }
    }

    private clsGeotabOptions ValidarConfiguracion()
    {
        var o = opciones.Value;
        if (!o.Configurado)
            throw new clsServiceUnavailableException(
                "GEOTAB_NO_CONFIGURADO",
                "Geotab no esta configurado: defina las variables Geotab__Database, Geotab__Username y Geotab__Password (y Geotab__Server si no es my.geotab.com).");
        return o;
    }

    private static string BaseUrl(string servidor) =>
        servidor.Contains("://", StringComparison.Ordinal) ? servidor.TrimEnd('/') : $"https://{servidor}";

    private static IEnumerable<JsonElement> Elementos(JsonElement resultado) =>
        resultado.ValueKind == JsonValueKind.Array ? resultado.EnumerateArray() : [];

    private static string? IdDispositivo(JsonElement e) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty("device", out var d) ? Texto(d, "id") : null;

    private static string? Texto(JsonElement e, string propiedad) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(propiedad, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    /// <summary>Solo numeros JSON (el origen exigia typeof === 'number'): un texto no cuenta como lectura.</summary>
    private static double? Numero(JsonElement e, string propiedad) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(propiedad, out var v)
        && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)
            ? d
            : null;

    private static DateTimeOffset? Fecha(JsonElement e, string propiedad) =>
        Texto(e, propiedad) is { } texto
        && DateTimeOffset.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var f)
            ? f
            : null;
}
