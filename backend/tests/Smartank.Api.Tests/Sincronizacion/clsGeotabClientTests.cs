using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartank.Application.Common.Exceptions;
using Smartank.Infrastructure.Sincronizacion.Adaptadores.Geotab;

namespace Smartank.Api.Tests.Sincronizacion;

/// <summary>Cliente de Geotab contra un servidor simulado (sin red): sesion cacheada, federacion y reautenticacion unica.</summary>
public class clsGeotabClientTests
{
    private sealed record clsLlamada(string Host, string Metodo, JsonElement Params);

    private sealed class clsServidorFalso(Func<clsLlamada, (HttpStatusCode Estado, string Cuerpo)> responder) : HttpMessageHandler
    {
        public List<clsLlamada> Llamadas { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            using var doc = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var llamada = new clsLlamada(request.RequestUri!.Host, doc.RootElement.GetProperty("method").GetString()!, doc.RootElement.GetProperty("params").Clone());
            lock (Llamadas) Llamadas.Add(llamada);
            await Task.Yield();
            var (estado, cuerpo) = responder(llamada);
            return new HttpResponseMessage(estado) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };
        }
    }

    private static clsGeotabClient Cliente(clsServidorFalso servidor, clsGeotabSesionCache? cache = null, bool configurado = true) => new(
        new HttpClient(servidor),
        cache ?? new clsGeotabSesionCache(),
        Options.Create(configurado
            ? new clsGeotabOptions { Database = "db", Username = "u", Password = "p", Server = "my.geotab.com" }
            : new clsGeotabOptions()),
        NullLogger<clsGeotabClient>.Instance);

    private static string Auth(string path = "ThisServer", string sesion = "S1") =>
        $$$"""{"result":{"credentials":{"database":"db","userName":"u","sessionId":"{{{sesion}}}"},"path":"{{{path}}}"}}""";

    [Fact]
    public async Task Sin_credenciales_lanza_GEOTAB_NO_CONFIGURADO_sin_hacer_ninguna_llamada()
    {
        var servidor = new clsServidorFalso(_ => (HttpStatusCode.OK, "{}"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Cliente(servidor, configurado: false).ListarDispositivosAsync());

        Assert.Equal("GEOTAB_NO_CONFIGURADO", ex.Code);
        Assert.Empty(servidor.Llamadas);
    }

    [Fact]
    public async Task Autentica_una_sola_vez_y_reutiliza_la_sesion_aun_con_llamadas_concurrentes()
    {
        var servidor = new clsServidorFalso(l => (HttpStatusCode.OK, l.Metodo == "Authenticate" ? Auth() : """{"result":[{"id":"b1","name":"ABC123"}]}"""));
        var c = Cliente(servidor);

        await Task.WhenAll(c.ListarDispositivosAsync(), c.ListarDispositivosAsync(), c.ObtenerEstadoDispositivosAsync(), c.ObtenerOdometroAsync(DateTimeOffset.UtcNow));

        Assert.Equal(1, servidor.Llamadas.Count(l => l.Metodo == "Authenticate"));
        Assert.Equal(4, servidor.Llamadas.Count(l => l.Metodo == "Get"));
        Assert.All(servidor.Llamadas.Where(l => l.Metodo == "Get"),
            l => Assert.Equal("S1", l.Params.GetProperty("credentials").GetProperty("sessionId").GetString()));
    }

    [Fact]
    public async Task Sigue_la_federacion_y_las_consultas_van_al_servidor_que_devolvio_path()
    {
        var servidor = new clsServidorFalso(l => (HttpStatusCode.OK, l.Metodo == "Authenticate" ? Auth("my379.geotab.com") : """{"result":[]}"""));

        await Cliente(servidor).ListarDispositivosAsync();

        Assert.Equal("my.geotab.com", servidor.Llamadas[0].Host);
        Assert.Equal("my379.geotab.com", servidor.Llamadas[1].Host);
    }

    [Fact]
    public async Task Ante_un_error_de_la_API_reautentica_UNA_vez_y_reintenta()
    {
        var gets = 0;
        var auths = 0;
        var servidor = new clsServidorFalso(l =>
        {
            if (l.Metodo == "Authenticate") return (HttpStatusCode.OK, Auth(sesion: "S" + ++auths));
            return ++gets == 1
                ? (HttpStatusCode.OK, """{"error":{"message":"InvalidUserException: sesion vencida"}}""")
                : (HttpStatusCode.OK, """{"result":[{"id":"b1","name":"ABC123"}]}""");
        });

        var dispositivos = await Cliente(servidor).ListarDispositivosAsync();

        Assert.Equal("b1", Assert.Single(dispositivos).Id);
        Assert.Equal(2, auths);
        var reintento = servidor.Llamadas.Last(l => l.Metodo == "Get");
        Assert.Equal("S2", reintento.Params.GetProperty("credentials").GetProperty("sessionId").GetString());
    }

    [Fact]
    public async Task Si_el_error_persiste_tras_reautenticar_se_propaga_sin_mas_reintentos()
    {
        var auths = 0;
        var servidor = new clsServidorFalso(l => l.Metodo == "Authenticate"
            ? (HttpStatusCode.OK, Auth(sesion: "S" + ++auths))
            : (HttpStatusCode.OK, """{"error":{"message":"siempre falla"}}"""));

        var ex = await Assert.ThrowsAsync<clsGeotabApiException>(() => Cliente(servidor).ListarDispositivosAsync());

        Assert.Contains("siempre falla", ex.Message);
        Assert.Equal(2, auths);
        Assert.Equal(2, servidor.Llamadas.Count(l => l.Metodo == "Get"));
    }

    [Fact]
    public async Task Una_falla_HTTP_se_traduce_a_GEOTAB_NO_DISPONIBLE()
    {
        var servidor = new clsServidorFalso(_ => (HttpStatusCode.BadGateway, "{}"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(() => Cliente(servidor).ListarDispositivosAsync());

        Assert.Equal("GEOTAB_NO_DISPONIBLE", ex.Code);
    }

    [Fact]
    public async Task Lee_StatusData_con_el_diagnostico_y_la_fecha_y_descarta_lecturas_incompletas()
    {
        var servidor = new clsServidorFalso(l => (HttpStatusCode.OK, l.Metodo == "Authenticate" ? Auth() : """
            {"result":[
              {"device":{"id":"b1"},"data":62.5,"dateTime":"2026-06-01T10:00:00.000Z"},
              {"device":{"id":"b2"},"data":"41","dateTime":"2026-06-01T10:00:00.000Z"},
              {"device":{"id":"b3"},"data":10},
              {"data":10,"dateTime":"2026-06-01T10:00:00.000Z"}
            ]}
            """));

        var desde = new DateTimeOffset(2026, 5, 25, 0, 0, 0, TimeSpan.Zero);
        var lecturas = await Cliente(servidor).ObtenerNivelCombustibleAsync(desde);

        var l1 = Assert.Single(lecturas);                   // el texto "41", la sin fecha y la sin dispositivo se descartan
        Assert.Equal(("b1", 62.5), (l1.DispositivoId, l1.Valor));
        Assert.Equal(new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero), l1.FechaUtc);

        var busqueda = servidor.Llamadas.Single(c => c.Metodo == "Get").Params.GetProperty("search");
        Assert.Equal("DiagnosticFuelLevelId", busqueda.GetProperty("diagnosticSearch").GetProperty("id").GetString());
        Assert.Equal("2026-05-25T00:00:00.000Z", busqueda.GetProperty("fromDate").GetString());
    }

    [Fact]
    public async Task Cada_metodo_consulta_su_diagnostico()
    {
        var servidor = new clsServidorFalso(l => (HttpStatusCode.OK, l.Metodo == "Authenticate" ? Auth() : """{"result":[]}"""));
        var c = Cliente(servidor);

        await c.ObtenerCombustibleUsadoAsync(DateTimeOffset.UtcNow);
        await c.ObtenerOdometroAsync(DateTimeOffset.UtcNow);
        await c.ObtenerEstadoDispositivosAsync();

        var gets = servidor.Llamadas.Where(l => l.Metodo == "Get").ToList();
        Assert.Equal("DiagnosticDeviceTotalFuelId", gets[0].Params.GetProperty("search").GetProperty("diagnosticSearch").GetProperty("id").GetString());
        Assert.Equal("DiagnosticOdometerId", gets[1].Params.GetProperty("search").GetProperty("diagnosticSearch").GetProperty("id").GetString());
        Assert.Equal("DeviceStatusInfo", gets[2].Params.GetProperty("typeName").GetString());
    }
}
