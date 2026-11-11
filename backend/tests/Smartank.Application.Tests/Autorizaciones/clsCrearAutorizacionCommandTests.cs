using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Application.Tests.Autorizaciones;

public class clsCrearAutorizacionCommandTests
{
    private static readonly Guid USUARIO = Guid.NewGuid();
    private static readonly DateTimeOffset INICIO = new(2026, 6, 17, 7, 0, 0, TimeSpan.Zero);

    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IContextoTanqueoRepository> _contexto = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();

    private readonly clsViaje _viaje;
    private readonly clsEstacion _estacion = clsDatosPrueba.Estacion();

    public clsCrearAutorizacionCommandTests()
    {
        _viaje = clsDatosPrueba.Viaje();
        _usuario.SetupGet(u => u.UsuarioId).Returns(USUARIO);
        _contexto.Setup(c => c.ObtenerViajeAsync(_viaje.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_viaje);
        _contexto.Setup(c => c.ObtenerEstacionAsync(_estacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_estacion);
    }

    private clsCrearAutorizacionCommandHandler Handler() =>
        new(_autorizaciones.Object, _contexto.Object, _auditoria.Object, _uow.Object, _usuario.Object);

    private clsCrearAutorizacionCommand Comando(
        Guid? viajeId = null, Guid? vehiculoId = null, Guid? estacionId = null, decimal? galones = 40m, decimal? precio = 9300m,
        decimal? ahorro = null, DateTimeOffset? inicio = null, DateTimeOffset? fin = null) => new(
        viajeId ?? _viaje.Id,
        vehiculoId ?? _viaje.VehiculoId,
        estacionId ?? _estacion.Id,
        galones,
        precio,
        ahorro,
        inicio ?? INICIO,
        fin ?? INICIO.AddHours(12));

    [Fact]
    public async Task Crea_la_propuesta_la_audita_y_la_devuelve()
    {
        clsAutorizacionTanqueo? agregada = null;
        _autorizaciones.Setup(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()))
            .Callback<clsAutorizacionTanqueo>(a => { a.Id = Guid.NewGuid(); agregada = a; });

        var dto = await Handler().Handle(Comando(ahorro: 15000m));

        Assert.NotNull(agregada);
        Assert.Equal(EstadoAutorizacion.Propuesta, agregada!.Estado);
        Assert.Equal(_viaje.Id, agregada.ViajeId);
        Assert.Equal(_viaje.VehiculoId, agregada.VehiculoId);
        Assert.Equal(_estacion.Id, agregada.EstacionId);
        Assert.Equal(40m, agregada.GalonesMax);
        Assert.Equal(9300m, agregada.PrecioTecho);
        Assert.Equal(15000m, agregada.DeltaAhorroEstimado);
        Assert.Equal(INICIO, agregada.FechaInicio);
        Assert.Equal(INICIO.AddHours(12), agregada.FechaFin);
        Assert.Equal(agregada.Id, dto.Id);
        Assert.Equal(EstadoAutorizacion.Propuesta, dto.Estado);
        Assert.Null(dto.AutorizadoPor);
        _auditoria.Verify(a => a.RegistrarAsync(
            USUARIO, "crear_autorizacion", "autorizaciones_tanqueo", agregada.Id,
            It.Is<object?>(d => clsDatosPrueba.Json(d) == "{\"estado\":\"propuesta\"}"),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_ventana_de_cero_duracion_es_valida()
    {
        _autorizaciones.Setup(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()));

        var dto = await Handler().Handle(Comando(inicio: INICIO, fin: INICIO));

        Assert.Equal(dto.FechaInicio, dto.FechaFin);
    }

    public static TheoryData<string, Func<clsCrearAutorizacionCommandTests, clsCrearAutorizacionCommand>> Invalidos => new()
    {
        { "sin viaje", t => t.Comando() with { ViajeId = null } },
        { "viaje vacio", t => t.Comando() with { ViajeId = Guid.Empty } },
        { "sin vehiculo", t => t.Comando() with { VehiculoId = null } },
        { "sin estacion", t => t.Comando() with { EstacionId = null } },
        { "sin galones", t => t.Comando(galones: null) },
        { "galones en cero", t => t.Comando(galones: 0m) },
        { "galones negativos", t => t.Comando(galones: -1m) },
        { "sin precio techo", t => t.Comando(precio: null) },
        { "precio negativo", t => t.Comando(precio: -1m) },
        { "sin fecha de inicio", t => t.Comando() with { FechaInicio = null } },
        { "sin fecha de fin", t => t.Comando() with { FechaFin = null } },
        { "fin anterior al inicio", t => t.Comando(inicio: INICIO.AddHours(12), fin: INICIO) },
    };

    [Theory]
    [MemberData(nameof(Invalidos))]
    public async Task Los_datos_invalidos_lanzan_400_AUTORIZACION_INVALIDA(
        string caso, Func<clsCrearAutorizacionCommandTests, clsCrearAutorizacionCommand> fabrica)
    {
        _ = caso;

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(fabrica(this)));

        Assert.Equal("AUTORIZACION_INVALIDA", ex.Code);
        _autorizaciones.Verify(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task Un_precio_techo_en_cero_es_valido()
    {
        _autorizaciones.Setup(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()));

        var dto = await Handler().Handle(Comando(precio: 0m));

        Assert.Equal(0m, dto.PrecioTecho);
    }

    [Fact]
    public async Task Viaje_inexistente_lanza_404()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(Comando(viajeId: Guid.NewGuid())));

        Assert.Equal("VIAJE_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Un_vehiculo_que_no_es_del_viaje_lanza_400()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(Comando(vehiculoId: Guid.NewGuid())));

        Assert.Equal("VEHICULO_NO_CORRESPONDE", ex.Code);
    }

    [Fact]
    public async Task Estacion_inexistente_lanza_404()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(Comando(estacionId: Guid.NewGuid())));

        Assert.Equal("ESTACION_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Si_el_viaje_ya_tiene_propuesta_lanza_409_PROPUESTA_EXISTENTE()
    {
        _autorizaciones.Setup(a => a.ObtenerPropuestaDeViajeAsync(_viaje.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(clsDatosPrueba.Autorizacion(viajeId: _viaje.Id));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("PROPUESTA_EXISTENTE", ex.Code);
        _autorizaciones.Verify(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task Ante_una_carrera_el_REGISTRO_DUPLICADO_se_traduce_a_PROPUESTA_EXISTENTE()
    {
        _auditoria.Setup(a => a.RegistrarAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("REGISTRO_DUPLICADO", "duplicado"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(Comando()));

        Assert.Equal("PROPUESTA_EXISTENTE", ex.Code);
    }
}
