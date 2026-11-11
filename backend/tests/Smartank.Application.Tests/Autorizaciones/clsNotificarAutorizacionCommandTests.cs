using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;
using Smartank.Application.CasosDeUso.Autorizaciones.Excepciones;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Conductores.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Application.Tests.Autorizaciones;

public class clsNotificarAutorizacionCommandTests
{
    private static readonly Guid USUARIO = Guid.NewGuid();
    private const string TOKEN = "TOKEN.DE.PRUEBA";

    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IContextoTanqueoRepository> _contexto = new();
    private readonly Mock<IBitacoraTokenPort> _tokens = new();
    private readonly Mock<IWhatsappPort> _whatsapp = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly clsAplicacionOptions _app = new() { UrlPublica = "https://smartank.test" };

    private readonly clsEstacion _estacion = clsDatosPrueba.Estacion(lat: 4.5, lng: -74.25, nombre: "EDS Norte");
    private readonly clsVehiculo _vehiculo = clsDatosPrueba.Vehiculo();
    private readonly clsViaje _viaje;
    private readonly clsAutorizacionTanqueo _autorizacion;
    private clsConductor? _conductor = clsDatosPrueba.Conductor("3106996633", "Conductor Uno");

    public clsNotificarAutorizacionCommandTests()
    {
        _viaje = clsDatosPrueba.Viaje(_vehiculo.Id);
        _autorizacion = clsDatosPrueba.Autorizacion(
            EstadoAutorizacion.Autorizada, 41m, _viaje.Id, _vehiculo.Id, _estacion.Id);

        _usuario.SetupGet(u => u.UsuarioId).Returns(USUARIO);
        _tokens.Setup(t => t.Emitir(_autorizacion.Id)).Returns(TOKEN);
        _whatsapp.SetupGet(w => w.EstaConfigurado).Returns(true);
        _whatsapp.SetupGet(w => w.ModoPrueba).Returns(false);
        _autorizaciones.Setup(r => r.ObtenerAsync(_autorizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_autorizacion);
        _contexto.Setup(c => c.ObtenerEstacionAsync(_estacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_estacion);
        _contexto.Setup(c => c.ObtenerVehiculoAsync(_vehiculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vehiculo);
        _contexto.Setup(c => c.ObtenerViajeAsync(_viaje.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_viaje);
        _contexto.Setup(c => c.ObtenerConductorPorCedulaAsync("80769132", It.IsAny<CancellationToken>())).ReturnsAsync(() => _conductor);
    }

    private clsNotificarAutorizacionCommandHandler Handler() => new(
        _autorizaciones.Object, _contexto.Object, _tokens.Object, _whatsapp.Object, _auditoria.Object, _usuario.Object,
        Options.Create(_app));

    private string? _numeroEnviado;
    private string? _textoEnviado;

    private void EnvioExitoso(string sid = "MSG-1") =>
        _whatsapp.Setup(w => w.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((n, t, _) => { _numeroEnviado = n; _textoEnviado = t; })
            .ReturnsAsync(sid);

    // ---- Camino feliz -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Envia_la_orden_al_celular_normalizado_del_conductor()
    {
        EnvioExitoso("MSG-1");

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Equal("+573106996633", _numeroEnviado);
        Assert.Equal("+573106996633", r.Destinatario.Telefono);
        Assert.Equal("Conductor Uno", r.Destinatario.Nombre);
        Assert.True(r.Whatsapp.Enviado);
        Assert.Equal("MSG-1", r.Whatsapp.Sid);
        Assert.Null(r.Whatsapp.Motivo);
        Assert.Equal(_autorizacion.Id, r.AutorizacionId);
    }

    [Fact]
    public async Task El_enlace_de_la_bitacora_lleva_el_token_firmado_y_no_el_manifiesto()
    {
        EnvioExitoso();

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Equal($"https://smartank.test/bitacora/{TOKEN}", r.BitacoraUrl);
        Assert.DoesNotContain(_viaje.Manifiesto!, r.BitacoraUrl);
        Assert.Contains(r.BitacoraUrl, _textoEnviado);
    }

    [Fact]
    public async Task La_url_publica_con_barra_final_no_duplica_la_barra()
    {
        _app.UrlPublica = "https://smartank.test/";
        EnvioExitoso();

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Equal($"https://smartank.test/bitacora/{TOKEN}", r.BitacoraUrl);
    }

    [Fact]
    public async Task La_orden_trae_placa_manifiesto_ruta_estacion_galones_y_mapa()
    {
        EnvioExitoso();

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Equal("ABC123", r.Orden.Placa);
        Assert.Equal("301457403", r.Orden.Manifiesto);
        Assert.Equal("Bogota - Medellin", r.Orden.Ruta);
        Assert.Equal("EDS Norte", r.Orden.Estacion);
        Assert.Equal(41m, r.Orden.GalonesAutorizados);
        Assert.Equal("https://www.google.com/maps?q=4.5,-74.25", r.Orden.MapaUrl);
        Assert.Contains("Placa: ABC123", _textoEnviado);
        Assert.Contains("Manifiesto: 301457403", _textoEnviado);
        Assert.Contains("Estación: EDS Norte", _textoEnviado);
        Assert.Contains("Galones autorizados: 41", _textoEnviado);
        Assert.Contains("Ubicación: https://www.google.com/maps?q=4.5,-74.25", _textoEnviado);
    }

    [Fact]
    public async Task Sin_coordenadas_de_la_estacion_la_orden_no_lleva_mapa()
    {
        _estacion.Lat = null;
        _estacion.Lng = null;
        EnvioExitoso();

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Null(r.Orden.MapaUrl);
        Assert.DoesNotContain("Ubicación", _textoEnviado);
    }

    [Fact]
    public async Task El_viaje_sin_manifiesto_se_notifica_igual_porque_el_enlace_usa_el_token()
    {
        _viaje.Manifiesto = null;
        EnvioExitoso();

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.True(r.Whatsapp.Enviado);
        Assert.Null(r.Orden.Manifiesto);
        Assert.DoesNotContain("Manifiesto", _textoEnviado);
    }

    // ---- Destinatario -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_celular_con_indicativo_se_normaliza_a_mas_57()
    {
        _conductor = clsDatosPrueba.Conductor("573106996633");
        EnvioExitoso();

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Equal("+573106996633", r.Destinatario.Telefono);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("80769132")]      // una cedula en el campo de celular
    [InlineData("2106996633")]    // no empieza por 3
    [InlineData("31069966")]      // incompleto
    public async Task Sin_celular_valido_lanza_409_CONDUCTOR_SIN_CELULAR_y_no_envia(string? celular)
    {
        _conductor = clsDatosPrueba.Conductor(celular);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));

        Assert.Equal("CONDUCTOR_SIN_CELULAR", ex.Code);
        _whatsapp.Verify(w => w.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Un_viaje_sin_conductor_lanza_409_CONDUCTOR_SIN_CELULAR()
    {
        _viaje.CedulaConductor = null;

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));

        Assert.Equal("CONDUCTOR_SIN_CELULAR", ex.Code);
    }

    [Fact]
    public async Task Un_conductor_inexistente_lanza_409_CONDUCTOR_SIN_CELULAR()
    {
        _conductor = null;

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));

        Assert.Equal("CONDUCTOR_SIN_CELULAR", ex.Code);
    }

    [Fact]
    public async Task Modo_prueba_redirige_al_numero_de_pruebas_aunque_el_conductor_no_tenga_celular()
    {
        _whatsapp.SetupGet(w => w.ModoPrueba).Returns(true);
        _whatsapp.SetupGet(w => w.NumeroPrueba).Returns("3001112233");
        _conductor = clsDatosPrueba.Conductor(celular: null);
        EnvioExitoso();

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Equal("+573001112233", _numeroEnviado);
        Assert.Equal("+573001112233", r.Destinatario.Telefono);
        Assert.Equal("Conductor", r.Destinatario.Nombre); // no se expone el nombre del conductor real
        // Ni siquiera se consulta al conductor: el modo de prueba ignora el celular real.
        _contexto.Verify(c => c.ObtenerConductorPorCedulaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Modo_prueba_con_celular_real_del_conductor_envia_igual_al_numero_de_pruebas()
    {
        _whatsapp.SetupGet(w => w.ModoPrueba).Returns(true);
        _whatsapp.SetupGet(w => w.NumeroPrueba).Returns("+573001112233");
        EnvioExitoso();

        await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.Equal("+573001112233", _numeroEnviado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]
    public async Task Modo_prueba_sin_un_numero_de_pruebas_valido_lanza_503_y_no_envia(string? numeroPrueba)
    {
        _whatsapp.SetupGet(w => w.ModoPrueba).Returns(true);
        _whatsapp.SetupGet(w => w.NumeroPrueba).Returns(numeroPrueba);

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(
            () => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));

        Assert.Equal("WHATSAPP_NUMERO_PRUEBA_INVALIDO", ex.Code);
        _whatsapp.Verify(w => w.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ---- WhatsApp no configurado / con error ---------------------------------------------------------------------------

    [Fact]
    public async Task Con_whatsapp_sin_configurar_devuelve_enviado_false_con_motivo_sin_fallar()
    {
        _whatsapp.SetupGet(w => w.EstaConfigurado).Returns(false);

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.False(r.Whatsapp.Enviado);
        Assert.Null(r.Whatsapp.Sid);
        Assert.Contains("WhatsApp no configurado", r.Whatsapp.Motivo);
        _whatsapp.Verify(w => w.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        // La orden queda preparada: destinatario y enlace igual se devuelven.
        Assert.Equal("+573106996633", r.Destinatario.Telefono);
        Assert.Equal($"https://smartank.test/bitacora/{TOKEN}", r.BitacoraUrl);
    }

    [Fact]
    public async Task Un_error_de_envio_se_reporta_en_el_resultado_sin_fallar_la_peticion()
    {
        _whatsapp.Setup(w => w.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsWhatsappEnvioException("No se pudo conectar a la API de WhatsApp [ConnectionError]."));

        var r = await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        Assert.False(r.Whatsapp.Enviado);
        Assert.Equal("No se pudo conectar a la API de WhatsApp [ConnectionError].", r.Whatsapp.Motivo);
    }

    [Fact]
    public async Task Una_cancelacion_del_llamador_no_se_confunde_con_un_error_de_envio()
    {
        _whatsapp.Setup(w => w.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));
    }

    // ---- Auditoria --------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Audita_el_envio_con_el_celular_enmascarado_y_sin_el_enlace()
    {
        EnvioExitoso();
        object? diff = null;
        _auditoria.Setup(a => a.RegistrarAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .Callback<Guid?, string, string, Guid?, object?, CancellationToken>((_, _, _, _, d, _) => diff = d);

        await Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id));

        _auditoria.Verify(a => a.RegistrarAsync(
            USUARIO, "notificar_conductor", "autorizaciones_tanqueo", _autorizacion.Id, It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
        var json = clsDatosPrueba.Json(diff);
        Assert.Contains("\"whatsapp_enviado\":true", json);
        Assert.Contains("\"destinatario\":\"*********6633\"", json);
        Assert.DoesNotContain("3106996633", json);
        Assert.DoesNotContain(TOKEN, json);
    }

    // ---- Precondiciones ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.Expirada)]
    public async Task Exige_estado_autorizada(EstadoAutorizacion estado)
    {
        _autorizacion.Estado = estado;

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));

        Assert.Equal("TRANSICION_INVALIDA", ex.Code);
        _tokens.Verify(t => t.Emitir(It.IsAny<Guid>()), Times.Never);
    }

    [Fact]
    public async Task Autorizacion_inexistente_lanza_404()
    {
        _autorizaciones.Setup(r => r.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAutorizacionTanqueo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsNotificarAutorizacionCommand(Guid.NewGuid())));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Sin_url_publica_lanza_503_y_no_envia(string? url)
    {
        _app.UrlPublica = url;

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(
            () => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));

        Assert.Equal("APP_URL_PUBLICA_NO_CONFIGURADA", ex.Code);
        _whatsapp.Verify(w => w.EnviarAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_el_secreto_de_la_bitacora_falta_el_503_del_token_se_propaga()
    {
        _tokens.Setup(t => t.Emitir(It.IsAny<Guid>()))
            .Throws(new clsServiceUnavailableException("BITACORA_SECRET_NO_CONFIGURADO", "falta"));

        var ex = await Assert.ThrowsAsync<clsServiceUnavailableException>(
            () => Handler().Handle(new clsNotificarAutorizacionCommand(_autorizacion.Id)));

        Assert.Equal("BITACORA_SECRET_NO_CONFIGURADO", ex.Code);
    }
}
