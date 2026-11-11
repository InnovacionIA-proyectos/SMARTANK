using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Infrastructure.Sincronizacion.Adaptadores;

/// <summary>
/// Precios reales de EDS desde las fuentes publicas de cada red. Terpel: GET del servicio de puntos. Distracom: POST de
/// ubicacion de estaciones, que exige la cookie de sesion de un GET previo a <c>/distraloc</c>, el Referer y la cabecera XHR.
/// Se normaliza a ACPM y corriente (COP/galon; null si la estacion no vende el producto o el precio sale de la banda realista).
/// </summary>
public sealed class clsPreciosHttpClient(HttpClient http, IOptions<clsPreciosOptions> opciones) : IPreciosEdsPort
{
    private const string USER_AGENT = "Mozilla/5.0";

    public async Task<IReadOnlyList<clsPrecioEdsDto>> ObtenerTerpelAsync(CancellationToken ct = default)
    {
        using var peticion = new HttpRequestMessage(HttpMethod.Get, opciones.Value.TerpelUrl);
        peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        peticion.Headers.UserAgent.ParseAdd(USER_AGENT);

        using var respuesta = await http.SendAsync(peticion, ct);
        if (!respuesta.IsSuccessStatusCode) throw new HttpRequestException($"Terpel respondio {(int)respuesta.StatusCode}.");

        using var doc = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return InterpretarTerpel(doc.RootElement);
    }

    public async Task<IReadOnlyList<clsPrecioEdsDto>> ObtenerDistracomAsync(CancellationToken ct = default)
    {
        var baseUrl = opciones.Value.DistracomUrl.TrimEnd('/');

        // 1) Sesion: el GET inicial entrega la cookie que el POST necesita (el handler no administra cookies solo).
        using var inicial = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/distraloc");
        inicial.Headers.UserAgent.ParseAdd(USER_AGENT);
        using var respuestaInicial = await http.SendAsync(inicial, ct);
        var cookies = respuestaInicial.Headers.TryGetValues("Set-Cookie", out var valores)
            ? string.Join("; ", valores.Select(v => v.Split(';')[0]))
            : string.Empty;

        // 2) Datos de estaciones: POST vacio con cookie, Referer y XHR.
        using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/distraloc/Home/GetUbicacionEstaciones")
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/x-www-form-urlencoded"),
        };
        peticion.Headers.UserAgent.ParseAdd(USER_AGENT);
        peticion.Headers.TryAddWithoutValidation("X-Requested-With", "XMLHttpRequest");
        peticion.Headers.Referrer = new Uri($"{baseUrl}/distraloc");
        if (cookies.Length > 0) peticion.Headers.TryAddWithoutValidation("Cookie", cookies);

        using var respuesta = await http.SendAsync(peticion, ct);
        if (!respuesta.IsSuccessStatusCode) throw new HttpRequestException($"Distracom respondio {(int)respuesta.StatusCode}.");

        using var doc = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
        return InterpretarDistracom(doc.RootElement);
    }

    internal static IReadOnlyList<clsPrecioEdsDto> InterpretarTerpel(JsonElement raiz)
    {
        var lista = new List<clsPrecioEdsDto>();
        // El servicio de Terpel responde HTTP 200 con un objeto de error ({"status":500,...}) cuando falla por dentro: no es
        // "cero estaciones", es un fallo de la fuente y asi debe registrarse (el origen tambien fallaba aqui).
        if (raiz.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException(
                $"Terpel no devolvio la lista de estaciones: {Texto(raiz, "message") ?? "respuesta inesperada"}.");

        foreach (var e in raiz.EnumerateArray())
        {
            // Sin coordenadas la estacion no sirve (no se cruza ni se mapea).
            if (Numero(e, "lat") is not { } lat || Numero(e, "lon") is not { } lon) continue;

            var (latValida, lngValida) = clsReglasPrecios.CoordenadaValida(lat, lon) ? ((double?)lat, (double?)lon) : (null, null);
            lista.Add(new clsPrecioEdsDto(
                FuenteTanqueo.Terpel, null, Texto(e, "nom"), latValida, lngValida,
                PrecioTerpel(e, "ACPM") ?? PrecioTerpel(e, "DIESEL"),   // ACPM, o Diesel como equivalente
                PrecioTerpel(e, "CORRIENTE")));
        }
        return lista;
    }

    internal static IReadOnlyList<clsPrecioEdsDto> InterpretarDistracom(JsonElement raiz)
    {
        var lista = new List<clsPrecioEdsDto>();
        if (raiz.ValueKind != JsonValueKind.Object || !raiz.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return lista;

        foreach (var e in data.EnumerateArray())
        {
            if (Numero(e, "Latitud") is not { } lat || Numero(e, "Longitud") is not { } lng) continue;

            var (latValida, lngValida) = clsReglasPrecios.CoordenadaValida(lat, lng) ? ((double?)lat, (double?)lng) : (null, null);
            lista.Add(new clsPrecioEdsDto(
                FuenteTanqueo.Distracom, Texto(e, "EstacionID"), Texto(e, "NombreEstacion"), latValida, lngValida,
                clsReglasPrecios.PrecioValido(Numero(e, "DIESEL")),   // Distracom: DIESEL = ACPM
                clsReglasPrecios.PrecioValido(Numero(e, "CORRIENTE"))));
        }
        return lista;
    }

    private static decimal? PrecioTerpel(JsonElement estacion, string producto)
    {
        if (!estacion.TryGetProperty("price", out var precios) || precios.ValueKind != JsonValueKind.Array) return null;
        foreach (var p in precios.EnumerateArray())
            if (string.Equals(Texto(p, "productName"), producto, StringComparison.OrdinalIgnoreCase))
                return clsReglasPrecios.PrecioValido(Numero(p, "retailPrice"));
        return null;
    }

    private static string? Texto(JsonElement e, string propiedad)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(propiedad, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.String => clsTexto.NullifyBlank(v.GetString()),
            JsonValueKind.Number => v.GetRawText(),
            _ => null,
        };
    }

    /// <summary>Numero JSON o texto numerico (el origen usaba Number(), que acepta ambos); null si falta o no es numerico.</summary>
    private static double? Numero(JsonElement e, string propiedad)
    {
        if (e.ValueKind != JsonValueKind.Object || !e.TryGetProperty(propiedad, out var v)) return null;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.TryGetDouble(out var d) ? d : null,
            JsonValueKind.String when double.TryParse(v.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var d) => d,
            _ => null,
        };
    }
}
