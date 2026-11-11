using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.Excepciones;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

namespace Smartank.Infrastructure.Autorizaciones.Adaptadores;

/// <summary>
/// Envio por Evolution API: POST a Whatsapp__ApiUrl con el header <c>apikey</c> y el cuerpo
/// <c>{ number, text, delay, presence: "composing" }</c>. La resiliencia (timeout, circuito) la agrega el cliente HTTP
/// registrado en <c>AddAutorizacionesInfrastructure</c>; los reintentos estan apagados porque reenviar un POST de mensaje
/// podria duplicar la orden al conductor.
/// </summary>
public sealed class clsEvolutionWhatsappAdapter(HttpClient http, IOptions<clsWhatsappOptions> options) : IWhatsappPort
{
    private const string PRESENCIA = "composing";
    private const int MAX_DETALLE_ERROR = 200;
    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web);

    public bool EstaConfigurado => options.Value.EstaConfigurado;

    public bool ModoPrueba => options.Value.ModoPrueba;

    public string? NumeroPrueba => options.Value.NumeroPrueba;

    public async Task<string> EnviarAsync(string numero, string texto, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.EstaConfigurado)
            throw new clsWhatsappEnvioException("WhatsApp no configurado (Whatsapp__ApiUrl y Whatsapp__ApiKey).");

        // La API espera solo digitos con indicativo (57XXXXXXXXXX), sin "+" ni "whatsapp:".
        var soloDigitos = new string(numero.Where(char.IsDigit).ToArray());

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, o.ApiUrl)
        {
            Content = JsonContent.Create(new clsCuerpoEnvio(soloDigitos, texto, o.DelayMs, PRESENCIA), options: JSON),
        };
        solicitud.Headers.Add("apikey", o.ApiKey);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await http.SendAsync(solicitud, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // lo cancelo el llamador: no es un fallo del servicio.
        }
        catch (Exception ex)
        {
            // Red caida, DNS, timeout (de la politica de resiliencia) o circuito abierto. No se incluye la URL en el mensaje
            // porque el motivo llega a la pantalla del usuario.
            var causa = ex is HttpRequestException h ? h.HttpRequestError.ToString() : ex.GetType().Name;
            throw new clsWhatsappEnvioException($"No se pudo conectar a la API de WhatsApp [{causa}].");
        }

        using (respuesta)
        {
            if (!respuesta.IsSuccessStatusCode)
            {
                var detalle = await LeerTextoAsync(respuesta, ct);
                throw new clsWhatsappEnvioException(
                    $"Envío WhatsApp falló ({(int)respuesta.StatusCode}): {Recortar(detalle)}");
            }
            return await LeerIdMensajeAsync(respuesta, ct);
        }
    }

    private static async Task<string> LeerTextoAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            return await respuesta.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return string.Empty;
        }
    }

    /// <summary>Evolution responde <c>{ key: { id } }</c> (o <c>{ id }</c>); si no viene, "ok".</summary>
    private static async Task<string> LeerIdMensajeAsync(HttpResponseMessage respuesta, CancellationToken ct)
    {
        try
        {
            using var doc = await JsonDocument.ParseAsync(await respuesta.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var raiz = doc.RootElement;
            if (raiz.ValueKind == JsonValueKind.Object)
            {
                if (raiz.TryGetProperty("key", out var key) && key.ValueKind == JsonValueKind.Object
                    && key.TryGetProperty("id", out var idClave) && idClave.ValueKind == JsonValueKind.String)
                    return idClave.GetString()!;
                if (raiz.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                    return id.GetString()!;
            }
        }
        catch (JsonException)
        {
            // Cuerpo vacio o no JSON: el envio fue exitoso igual (2xx).
        }
        return "ok";
    }

    private static string Recortar(string texto) =>
        texto.Length <= MAX_DETALLE_ERROR ? texto : texto[..MAX_DETALLE_ERROR];

    private sealed record clsCuerpoEnvio(string Number, string Text, int Delay, string Presence);
}
