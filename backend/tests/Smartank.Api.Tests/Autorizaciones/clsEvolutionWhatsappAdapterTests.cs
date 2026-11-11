using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.Excepciones;
using Smartank.Infrastructure.Autorizaciones.Adaptadores;

namespace Smartank.Api.Tests.Autorizaciones;

public class clsEvolutionWhatsappAdapterTests
{
    private sealed class clsManejador(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public HttpRequestMessage? Solicitud { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Solicitud = request;
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return await responder(request);
        }
    }

    private readonly clsWhatsappOptions _opciones = new()
    {
        ApiUrl = "https://evolution.test/message/sendText/inst",
        ApiKey = "llave-de-prueba",
        DelayMs = 1200,
    };

    private clsEvolutionWhatsappAdapter Adaptador(clsManejador manejador) => new(new HttpClient(manejador), Options.Create(_opciones));

    private static Task<HttpResponseMessage> Ok(string json = "{\"key\":{\"id\":\"MSG-9\"}}") =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created) { Content = new StringContent(json) });

    [Fact]
    public async Task Envia_un_POST_con_apikey_y_el_cuerpo_de_evolution()
    {
        var m = new clsManejador(_ => Ok());

        var sid = await Adaptador(m).EnviarAsync("+57 310-699-6633", "hola\norden", CancellationToken.None);

        Assert.Equal("MSG-9", sid);
        Assert.Equal(HttpMethod.Post, m.Solicitud!.Method);
        Assert.Equal("https://evolution.test/message/sendText/inst", m.Solicitud.RequestUri!.ToString());
        Assert.Equal("llave-de-prueba", m.Solicitud.Headers.GetValues("apikey").Single());
        using var json = JsonDocument.Parse(m.Cuerpo!);
        Assert.Equal("573106996633", json.RootElement.GetProperty("number").GetString()); // solo digitos
        Assert.Equal("hola\norden", json.RootElement.GetProperty("text").GetString());
        Assert.Equal(1200, json.RootElement.GetProperty("delay").GetInt32());
        Assert.Equal("composing", json.RootElement.GetProperty("presence").GetString());
    }

    [Fact]
    public async Task El_id_puede_venir_en_la_raiz_o_faltar()
    {
        Assert.Equal("RAIZ-1", await Adaptador(new clsManejador(_ => Ok("{\"id\":\"RAIZ-1\"}"))).EnviarAsync("573106996633", "x", default));
        Assert.Equal("ok", await Adaptador(new clsManejador(_ => Ok("{}"))).EnviarAsync("573106996633", "x", default));
        Assert.Equal("ok", await Adaptador(new clsManejador(_ => Ok(""))).EnviarAsync("573106996633", "x", default));
        Assert.Equal("ok", await Adaptador(new clsManejador(_ => Ok("no es json"))).EnviarAsync("573106996633", "x", default));
    }

    [Fact]
    public async Task Un_error_http_se_reporta_con_el_codigo_y_un_detalle_recortado()
    {
        var m = new clsManejador(_ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(new string('x', 500)) }));

        var ex = await Assert.ThrowsAsync<clsWhatsappEnvioException>(() => Adaptador(m).EnviarAsync("573106996633", "x", default));

        Assert.StartsWith("Envío WhatsApp falló (400): ", ex.Message);
        Assert.Equal("Envío WhatsApp falló (400): ".Length + 200, ex.Message.Length);
        Assert.Equal("WHATSAPP_ENVIO_FALLIDO", ex.Code);
    }

    [Fact]
    public async Task Una_falla_de_red_da_un_mensaje_claro_sin_exponer_la_url_ni_la_llave()
    {
        var m = new clsManejador(_ => throw new HttpRequestException("dns"));

        var ex = await Assert.ThrowsAsync<clsWhatsappEnvioException>(() => Adaptador(m).EnviarAsync("573106996633", "x", default));

        Assert.StartsWith("No se pudo conectar a la API de WhatsApp", ex.Message);
        Assert.DoesNotContain("evolution.test", ex.Message);
        Assert.DoesNotContain("llave-de-prueba", ex.Message);
    }

    [Fact]
    public async Task Un_timeout_de_la_politica_de_resiliencia_tambien_es_un_error_de_envio()
    {
        var m = new clsManejador(_ => throw new TaskCanceledException("timeout"));

        await Assert.ThrowsAsync<clsWhatsappEnvioException>(() => Adaptador(m).EnviarAsync("573106996633", "x", CancellationToken.None));
    }

    [Fact]
    public async Task La_cancelacion_del_llamador_no_se_convierte_en_error_de_envio()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var m = new clsManejador(_ => throw new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Adaptador(m).EnviarAsync("573106996633", "x", cts.Token));
    }

    [Theory]
    [InlineData(null, "k")]
    [InlineData("https://x.test", null)]
    [InlineData("", "k")]
    [InlineData("https://x.test", " ")]
    public async Task Sin_url_o_llave_no_se_intenta_enviar(string? url, string? llave)
    {
        _opciones.ApiUrl = url;
        _opciones.ApiKey = llave;
        var m = new clsManejador(_ => Ok());

        Assert.False(Adaptador(m).EstaConfigurado);
        await Assert.ThrowsAsync<clsWhatsappEnvioException>(() => Adaptador(m).EnviarAsync("573106996633", "x", default));
        Assert.Null(m.Solicitud);
    }

    [Fact]
    public void Expone_modo_prueba_y_numero_de_prueba_desde_las_opciones()
    {
        _opciones.ModoPrueba = true;
        _opciones.NumeroPrueba = "3001112233";
        var a = Adaptador(new clsManejador(_ => Ok()));

        Assert.True(a.EstaConfigurado);
        Assert.True(a.ModoPrueba);
        Assert.Equal("3001112233", a.NumeroPrueba);
    }

    // ---- Validacion de arranque (la usa el orquestador) ------------------------------------------------------------

    [Fact]
    public void El_modo_prueba_en_produccion_no_arranca()
    {
        var o = new clsWhatsappOptions { ModoPrueba = true, NumeroPrueba = "3001112233" };

        Assert.Throws<InvalidOperationException>(() => o.Validar(esProduccion: true));
    }

    [Fact]
    public void El_modo_prueba_fuera_de_produccion_con_numero_es_valido()
    {
        new clsWhatsappOptions { ModoPrueba = true, NumeroPrueba = "3001112233" }.Validar(esProduccion: false);
    }

    [Fact]
    public void El_modo_prueba_sin_numero_no_arranca_en_ningun_entorno()
    {
        Assert.Throws<InvalidOperationException>(() => new clsWhatsappOptions { ModoPrueba = true }.Validar(esProduccion: false));
    }

    [Fact]
    public void Con_el_modo_prueba_apagado_produccion_arranca_y_los_defectos_son_los_documentados()
    {
        var o = new clsWhatsappOptions();
        o.Validar(esProduccion: true);

        Assert.False(o.ModoPrueba);
        Assert.Equal(1200, o.DelayMs);
        Assert.False(o.EstaConfigurado);
    }
}
