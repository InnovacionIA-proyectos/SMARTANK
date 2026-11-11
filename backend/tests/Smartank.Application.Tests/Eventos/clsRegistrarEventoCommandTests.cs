using Microsoft.Extensions.Logging;
using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.CasosDeUso.Eventos;
using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Application.Tests.Autorizaciones;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Application.Tests.Eventos;

public class clsRegistrarEventoCommandTests
{
    private const string TOKEN = "tok";

    private readonly Mock<IBitacoraTokenPort> _tokens = new();
    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IContextoTanqueoRepository> _contexto = new();
    private readonly Mock<IEventosRepository> _eventos = new();
    private readonly Mock<ICommandHandler<clsVerificarEventoCommand, clsVerificacionDto>> _verificar = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ILogger<clsRegistrarEventoCommandHandler>> _logger = new();
    private readonly clsRelojFijo _reloj = new(clsDatosPrueba.AHORA);

    private readonly clsVehiculo _vehiculo = clsDatosPrueba.Vehiculo(programa: "TM_MED-CTG SYT", tipoMotor: "diesel");
    private readonly clsAutorizacionTanqueo _autorizacion;
    private clsEventoTanqueo? _agregado;

    public clsRegistrarEventoCommandTests()
    {
        _autorizacion = clsDatosPrueba.Autorizacion(EstadoAutorizacion.Autorizada, 40m, vehiculoId: _vehiculo.Id);
        _tokens.Setup(t => t.Validar(TOKEN)).Returns(_autorizacion.Id);
        _autorizaciones.Setup(a => a.ObtenerAsync(_autorizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_autorizacion);
        _contexto.Setup(c => c.ObtenerVehiculoAsync(_vehiculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vehiculo);
        _eventos.Setup(e => e.Agregar(It.IsAny<clsEventoTanqueo>()))
            .Callback<clsEventoTanqueo>(e => { e.Id = Guid.NewGuid(); _agregado = e; });
        _verificar.Setup(v => v.Handle(It.IsAny<clsVerificarEventoCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsVerificacionDto(Guid.NewGuid(), EstadoAutorizacion.Ejecutada, 0, []));
    }

    private clsRegistrarEventoCommandHandler Handler() => new(
        _tokens.Object, _autorizaciones.Object, _contexto.Object, _eventos.Object, _verificar.Object,
        _auditoria.Object, _uow.Object, _reloj, _logger.Object);

    private static clsRegistrarEventoCommand Comando(
        string? token = TOKEN, decimal? galones = 40m, decimal? precio = 9300m, string? foto = null,
        double? odometro = 123456, decimal? antes = 10m, decimal? despues = 50m) =>
        new(token, galones, precio, foto, odometro, antes, despues);

    // ---- Camino feliz -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task Registra_el_evento_con_programa_y_producto_del_vehiculo_sin_registrador()
    {
        var dto = await Handler().Handle(Comando(foto: "https://app.test/api/uploads/abc"));

        Assert.NotNull(_agregado);
        Assert.Equal(_autorizacion.Id, _agregado!.AutorizacionId);
        Assert.Equal(_autorizacion.VehiculoId, _agregado.VehiculoId);
        Assert.Equal(_autorizacion.EstacionId, _agregado.EstacionId);
        Assert.Equal(40m, _agregado.GalonesCargados);
        Assert.Equal(9300m, _agregado.PrecioPagado);
        Assert.Equal(123456, _agregado.OdometroEvento);
        Assert.Equal(10m, _agregado.NivelAntes);
        Assert.Equal(50m, _agregado.NivelDespues);
        Assert.Equal("https://app.test/api/uploads/abc", _agregado.FotoSurtidorUrl);
        Assert.Null(_agregado.RegistradoPor);
        Assert.Equal("TM_MED-CTG SYT", _agregado.Programa);
        Assert.Equal("diesel", _agregado.Producto);
        Assert.Equal(clsDatosPrueba.AHORA, _agregado.OcurridoAt);
        Assert.Equal(_agregado.Id, dto.Id);
        Assert.Equal(40m, dto.GalonesCargados);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Audita_el_registro_sin_actor_porque_el_conductor_no_tiene_cuenta()
    {
        await Handler().Handle(Comando());

        _auditoria.Verify(a => a.RegistrarAsync(
            null, "registrar_evento", "eventos_tanqueo", _agregado!.Id,
            It.Is<object?>(d => clsDatosPrueba.Json(d) == "{\"galones_cargados\":40}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Los_campos_opcionales_pueden_ir_vacios_y_la_foto_en_blanco_queda_null()
    {
        await Handler().Handle(Comando(precio: null, foto: "   ", odometro: null, antes: null, despues: null));

        Assert.Null(_agregado!.PrecioPagado);
        Assert.Null(_agregado.FotoSurtidorUrl);
        Assert.Null(_agregado.OdometroEvento);
        Assert.Null(_agregado.NivelAntes);
    }

    [Fact]
    public async Task Tras_guardar_dispara_la_verificacion_del_evento()
    {
        var dto = await Handler().Handle(Comando());

        _verificar.Verify(v => v.Handle(
            It.Is<clsVerificarEventoCommand>(c => c.EventoId == dto.Id), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Si_la_verificacion_falla_el_evento_queda_guardado_y_el_error_se_registra()
    {
        _verificar.Setup(v => v.Handle(It.IsAny<clsVerificarEventoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var dto = await Handler().Handle(Comando());

        Assert.Equal(_agregado!.Id, dto.Id); // la respuesta es 201 igual
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _logger.Verify(l => l.Log(
            LogLevel.Error, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(),
            It.IsAny<InvalidOperationException>(), It.IsAny<Func<It.IsAnyType, Exception?, string>>()), Times.Once);
    }

    [Fact]
    public async Task La_cancelacion_durante_la_verificacion_no_se_traga()
    {
        _verificar.Setup(v => v.Handle(It.IsAny<clsVerificarEventoCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        await Assert.ThrowsAsync<OperationCanceledException>(() => Handler().Handle(Comando()));
    }

    // ---- Token ------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("BITACORA_TOKEN_INVALIDO")]
    [InlineData("BITACORA_TOKEN_EXPIRADO")]
    public async Task Un_token_invalido_o_expirado_lanza_401_sin_leer_nada_mas(string codigo)
    {
        _tokens.Setup(t => t.Validar(It.IsAny<string>())).Throws(new clsUnauthorizedException(codigo, "x"));

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() => Handler().Handle(Comando(token: "malo")));

        Assert.Equal(codigo, ex.Code);
        _autorizaciones.Verify(a => a.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _eventos.Verify(e => e.Agregar(It.IsAny<clsEventoTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task Un_token_ausente_se_trata_como_invalido()
    {
        _tokens.Setup(t => t.Validar(string.Empty)).Throws(new clsUnauthorizedException("BITACORA_TOKEN_INVALIDO", "x"));

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() => Handler().Handle(Comando(token: null)));

        Assert.Equal("BITACORA_TOKEN_INVALIDO", ex.Code);
    }

    // ---- Reglas de la bitacora --------------------------------------------------------------------------------------

    [Fact]
    public async Task Si_ya_hay_un_evento_lanza_409_BITACORA_YA_REGISTRADA()
    {
        _eventos.Setup(e => e.ExistePorAutorizacionAsync(_autorizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("BITACORA_YA_REGISTRADA", ex.Code);
        _eventos.Verify(e => e.Agregar(It.IsAny<clsEventoTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task Un_reintento_tras_la_verificacion_recibe_ya_registrada_y_no_un_error_de_estado()
    {
        // Tras verificar, la autorizacion quedo Ejecutada: aun asi el mensaje correcto es "ya registrada".
        _autorizacion.Estado = EstadoAutorizacion.Ejecutada;
        _eventos.Setup(e => e.ExistePorAutorizacionAsync(_autorizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("BITACORA_YA_REGISTRADA", ex.Code);
    }

    [Fact]
    public async Task Ante_una_carrera_el_REGISTRO_DUPLICADO_se_traduce_a_BITACORA_YA_REGISTRADA()
    {
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("REGISTRO_DUPLICADO", "dup"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("BITACORA_YA_REGISTRADA", ex.Code);
        _verificar.Verify(v => v.Handle(It.IsAny<clsVerificarEventoCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Expirada)]
    [InlineData(EstadoAutorizacion.Rechazada)]
    [InlineData(EstadoAutorizacion.Ejecutada)]
    public async Task Solo_se_registra_con_la_autorizacion_autorizada(EstadoAutorizacion estado)
    {
        _autorizacion.Estado = estado;

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("AUTORIZACION_NO_AUTORIZADA", ex.Code);
        _eventos.Verify(e => e.Agregar(It.IsAny<clsEventoTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task Antes_del_inicio_de_la_ventana_lanza_409_fuera_de_ventana()
    {
        _autorizacion.FechaInicio = clsDatosPrueba.AHORA.AddMinutes(1);
        _autorizacion.FechaFin = clsDatosPrueba.AHORA.AddHours(12);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("AUTORIZACION_FUERA_DE_VENTANA", ex.Code);
    }

    [Fact]
    public async Task Despues_del_fin_de_la_ventana_lanza_409_fuera_de_ventana()
    {
        _autorizacion.FechaInicio = clsDatosPrueba.AHORA.AddHours(-12);
        _autorizacion.FechaFin = clsDatosPrueba.AHORA.AddSeconds(-1);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("AUTORIZACION_FUERA_DE_VENTANA", ex.Code);
    }

    [Fact]
    public async Task Justo_en_el_fin_de_la_ventana_todavia_se_acepta()
    {
        _autorizacion.FechaInicio = clsDatosPrueba.AHORA.AddHours(-12);
        _autorizacion.FechaFin = clsDatosPrueba.AHORA;

        var dto = await Handler().Handle(Comando());

        Assert.Equal(40m, dto.GalonesCargados);
    }

    [Fact]
    public async Task La_ventana_se_juzga_con_el_reloj_actual_y_pasa_a_fuera_al_avanzar_el_tiempo()
    {
        _autorizacion.FechaFin = clsDatosPrueba.AHORA.AddHours(1);
        _reloj.Avanzar(TimeSpan.FromHours(2));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("AUTORIZACION_FUERA_DE_VENTANA", ex.Code);
    }

    [Fact]
    public async Task Autorizacion_inexistente_lanza_404()
    {
        _autorizaciones.Setup(a => a.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAutorizacionTanqueo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(Comando()));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Vehiculo_inexistente_lanza_404()
    {
        _contexto.Setup(c => c.ObtenerVehiculoAsync(_vehiculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync((clsVehiculo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(Comando()));

        Assert.Equal("VEHICULO_NO_ENCONTRADO", ex.Code);
    }

    // ---- Validacion de los datos ------------------------------------------------------------------------------------

    public static TheoryData<string, clsRegistrarEventoCommand> Invalidos => new()
    {
        { "sin galones", Comando(galones: null) },
        { "galones negativos", Comando(galones: -1m) },
        { "galones absurdos", Comando(galones: 10_000_000m) },
        { "precio negativo", Comando(precio: -1m) },
        { "precio absurdo", Comando(precio: 10_000_000_000m) },
        { "odometro negativo", Comando(odometro: -1) },
        { "odometro absurdo", Comando(odometro: 1_000_000_000) },
        { "nivel antes negativo", Comando(antes: -1m) },
        { "nivel despues absurdo", Comando(despues: 10_000_000m) },
        { "foto no es url", Comando(foto: "no es una url") },
        { "foto javascript", Comando(foto: "javascript:alert(1)") },
        { "foto file", Comando(foto: "file:///etc/passwd") },
        { "foto demasiado larga", Comando(foto: "https://x.test/" + new string('a', 500)) },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task Los_datos_invalidos_lanzan_400_EVENTO_INVALIDO(string caso, clsRegistrarEventoCommand comando)
    {
        _ = caso;

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(comando));

        Assert.Equal("EVENTO_INVALIDO", ex.Code);
        _eventos.Verify(e => e.Agregar(It.IsAny<clsEventoTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task Cero_galones_cargados_es_un_reporte_valido()
    {
        var dto = await Handler().Handle(Comando(galones: 0m));

        Assert.Equal(0m, dto.GalonesCargados);
    }
}
