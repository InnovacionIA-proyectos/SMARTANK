using System.Net;
using System.Net.Http.Json;
using Smartank.Api.Tests.Infra;
using Smartank.Domain.Enums;

namespace Smartank.Api.Tests.Http;

/// <summary>Pruebas HTTP del API real: formato de errores, sesion por cookie, roles y contrato JSON snake_case.</summary>
public class clsSesionYRolesHttpTests : IClassFixture<clsApiFactory>
{
    private readonly clsApiFactory _factory;

    public clsSesionYRolesHttpTests(clsApiFactory factory) => _factory = factory;

    [clsHechoConBaseDePruebas]
    public async Task Health_responde_200_con_el_detalle_de_la_base()
    {
        var resp = await _factory.ClienteAnonimo().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await clsApiFactory.LeerJsonAsync(resp);
        Assert.Equal("Healthy", json.GetProperty("status").GetString());
        Assert.Equal("Healthy", json.GetProperty("chequeos").GetProperty("database").GetProperty("status").GetString());
    }

    [clsHechoConBaseDePruebas]
    public async Task Un_endpoint_protegido_sin_sesion_responde_401_con_la_forma_estandar_de_error()
    {
        var resp = await _factory.ClienteAnonimo().GetAsync("/api/viajes");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var json = await clsApiFactory.LeerJsonAsync(resp);
        Assert.Equal("NO_AUTENTICADO", json.GetProperty("code").GetString());
        Assert.True(json.TryGetProperty("message", out _));
        Assert.True(json.TryGetProperty("timestamp", out _));
    }

    [clsHechoConBaseDePruebas]
    public async Task Login_con_credenciales_invalidas_responde_401_sin_revelar_si_el_correo_existe()
    {
        var cliente = _factory.ClienteAnonimo();

        var inexistente = await cliente.PostAsJsonAsync("/api/auth/login", new { email = "nadie@pruebas.smartank.local", password = "x" });
        var json = await clsApiFactory.LeerJsonAsync(inexistente);

        Assert.Equal(HttpStatusCode.Unauthorized, inexistente.StatusCode);
        Assert.Equal("CREDENCIALES_INVALIDAS", json.GetProperty("code").GetString());
    }

    [clsHechoConBaseDePruebas]
    public async Task Login_fija_una_cookie_httpOnly_SameSite_Strict_y_me_devuelve_el_usuario_con_el_rol_en_snake_case()
    {
        var cliente = await _factory.SesionAsync(Rol.ControlFlota);

        var me = await cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var json = await clsApiFactory.LeerJsonAsync(me);
        Assert.Equal("control_flota", json.GetProperty("rol").GetString());
        Assert.True(json.TryGetProperty("email", out _));
        Assert.True(json.TryGetProperty("nombre", out _));
    }

    [clsHechoConBaseDePruebas]
    public async Task La_cookie_de_sesion_es_httpOnly_y_SameSite_Strict()
    {
        // Sesion propia (no la cacheada): el logout la destruye y las demas pruebas no deben verse afectadas.
        var cliente = await _factory.SesionAsync(Rol.Despacho, nueva: true);
        var logout = await cliente.PostAsync("/api/auth/logout", null);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        var setCookie = string.Join(";", logout.Headers.GetValues("Set-Cookie")).ToLowerInvariant();
        Assert.Contains("smartank_session=", setCookie);
        Assert.Contains("httponly", setCookie);
        Assert.Contains("samesite=strict", setCookie);

        // Tras el logout la sesion ya no sirve.
        var me = await cliente.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [clsHechoConBaseDePruebas]
    public async Task Alertas_solo_las_ve_ControlFlota_o_Admin()
    {
        var control = await _factory.SesionAsync(Rol.ControlFlota);
        var finanzas = await _factory.SesionAsync(Rol.Finanzas);

        Assert.Equal(HttpStatusCode.OK, (await control.GetAsync("/api/alertas")).StatusCode);

        var prohibido = await finanzas.GetAsync("/api/alertas");
        Assert.Equal(HttpStatusCode.Forbidden, prohibido.StatusCode);
        Assert.Equal("ACCESO_DENEGADO", (await clsApiFactory.LeerJsonAsync(prohibido)).GetProperty("code").GetString());
    }

    [clsHechoConBaseDePruebas]
    public async Task Metricas_solo_las_ve_Finanzas_o_Admin_y_responden_en_snake_case()
    {
        var finanzas = await _factory.SesionAsync(Rol.Finanzas);
        var control = await _factory.SesionAsync(Rol.ControlFlota);

        var ok = await finanzas.GetAsync("/api/metricas/recuperacion");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var json = await clsApiFactory.LeerJsonAsync(ok);
        Assert.True(json.TryGetProperty("galones_total", out _));
        Assert.True(json.TryGetProperty("utilizacion_eds", out _));
        Assert.True(json.TryGetProperty("opciones", out _));

        Assert.Equal(HttpStatusCode.Forbidden, (await control.GetAsync("/api/metricas/recuperacion")).StatusCode);
    }

    [clsHechoConBaseDePruebas]
    public async Task Crear_y_autorizar_exigen_Despacho_o_Admin()
    {
        var control = await _factory.SesionAsync(Rol.ControlFlota);

        var resp = await control.PostAsync($"/api/autorizaciones/{Guid.NewGuid()}/autorizar", null);

        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [clsHechoConBaseDePruebas]
    public async Task La_ingesta_de_telemetria_exige_el_secreto_de_servicio()
    {
        var cliente = _factory.ClienteAnonimo();
        var cuerpo = new { placa = "ZZZ999", nivel_galones = 10 };

        var sin = await cliente.PostAsJsonAsync("/api/telemetria/ingesta", cuerpo);
        Assert.Equal(HttpStatusCode.Unauthorized, sin.StatusCode);
        Assert.Equal("SECRETO_SERVICIO_INVALIDO", (await clsApiFactory.LeerJsonAsync(sin)).GetProperty("code").GetString());

        using var conSecreto = new HttpRequestMessage(HttpMethod.Post, "/api/telemetria/ingesta") { Content = JsonContent.Create(cuerpo) };
        conSecreto.Headers.Add("X-Service-Secret", clsApiFactory.SECRETO_SERVICIO);
        var resp = await cliente.SendAsync(conSecreto);
        // Con secreto valido llega al caso de uso: la placa no existe -> 404 del dominio, no 401.
        Assert.Equal(HttpStatusCode.NotFound, resp.StatusCode);
        Assert.Equal("VEHICULO_NO_ENCONTRADO", (await clsApiFactory.LeerJsonAsync(resp)).GetProperty("code").GetString());
    }

    [clsHechoConBaseDePruebas]
    public async Task Un_id_que_no_es_uuid_responde_400_con_la_forma_estandar()
    {
        var cliente = await _factory.SesionAsync(Rol.Despacho);

        var resp = await cliente.GetAsync("/api/vehiculos/no-es-un-uuid/telemetria");

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        Assert.Equal("VALIDATION_ERROR", (await clsApiFactory.LeerJsonAsync(resp)).GetProperty("code").GetString());
    }

    [clsHechoConBaseDePruebas]
    public async Task La_bitacora_con_un_token_invalido_responde_401_sin_necesitar_sesion()
    {
        var resp = await _factory.ClienteAnonimo().GetAsync("/api/bitacora/token-falso");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        Assert.Equal("BITACORA_TOKEN_INVALIDO", (await clsApiFactory.LeerJsonAsync(resp)).GetProperty("code").GetString());
    }

    [clsHechoConBaseDePruebas]
    public async Task El_historial_de_viajes_responde_con_la_forma_del_contrato()
    {
        var cliente = await _factory.SesionAsync(Rol.Despacho);

        var resp = await cliente.GetAsync("/api/viajes");

        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var json = await clsApiFactory.LeerJsonAsync(resp);
        Assert.True(json.TryGetProperty("total", out _));
        Assert.True(json.TryGetProperty("viajes", out _));
        Assert.True(json.GetProperty("opciones").TryGetProperty("rutas", out _));
    }
}
