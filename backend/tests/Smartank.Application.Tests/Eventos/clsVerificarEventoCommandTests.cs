using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Eventos;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Application.Tests.Autorizaciones;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Eventos.Entidades;

namespace Smartank.Application.Tests.Eventos;

/// <summary>Cruce del evento contra la autorizacion; los umbrales exactos de cada alerta se prueban en el Domain (clsVerificacionCargaTests).</summary>
public class clsVerificarEventoCommandTests
{
    private static readonly Guid USUARIO = Guid.NewGuid();

    private readonly Mock<IEventosRepository> _eventos = new();
    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IContextoTanqueoRepository> _contexto = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly clsParametrosNegocio _parametros = new();

    private readonly clsAutorizacionTanqueo _autorizacion;
    private readonly clsEstacion _estacion = clsDatosPrueba.Estacion(precioAcpm: 9500m, ruta: "Bogota - Medellin");
    private readonly clsEventoTanqueo _evento;
    private readonly List<clsAlerta> _alertas = [];

    public clsVerificarEventoCommandTests()
    {
        _autorizacion = clsDatosPrueba.Autorizacion(EstadoAutorizacion.Autorizada, 40m, estacionId: _estacion.Id);
        _evento = new clsEventoTanqueo
        {
            Id = Guid.NewGuid(),
            AutorizacionId = _autorizacion.Id,
            VehiculoId = _autorizacion.VehiculoId,
            EstacionId = _estacion.Id,
            GalonesCargados = 40m,
        };
        _usuario.SetupGet(u => u.UsuarioId).Returns(USUARIO);
        _eventos.Setup(e => e.ObtenerAsync(_evento.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_evento);
        _eventos.Setup(e => e.AgregarAlerta(It.IsAny<clsAlerta>())).Callback<clsAlerta>(a => { a.Id = Guid.NewGuid(); _alertas.Add(a); });
        _eventos.Setup(e => e.ObtenerPrecioMinimoDeRutaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(9500m);
        _autorizaciones.Setup(a => a.ObtenerAsync(_autorizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_autorizacion);
        _contexto.Setup(c => c.ObtenerEstacionAsync(_estacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_estacion);
    }

    private clsVerificarEventoCommandHandler Handler() => new(
        _eventos.Object, _autorizaciones.Object, _contexto.Object, _auditoria.Object, _uow.Object, _usuario.Object,
        Options.Create(_parametros));

    [Fact]
    public async Task Una_carga_completa_deja_la_autorizacion_ejecutada_sin_alertas()
    {
        _evento.NivelAntes = 10m;
        _evento.NivelDespues = 50m;

        var r = await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        Assert.Equal(EstadoAutorizacion.Ejecutada, r.EstadoAutorizacion);
        Assert.Equal(EstadoAutorizacion.Ejecutada, _autorizacion.Estado);
        Assert.Equal(0, r.AlertasGeneradas);
        Assert.Empty(_alertas);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Desviacion_de_carga_crea_la_alerta_ligada_al_evento_y_al_vehiculo()
    {
        // Cargo 40, el nivel subio 15: faltante 25.
        _evento.NivelAntes = 10m;
        _evento.NivelDespues = 25m;

        var r = await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        var alerta = Assert.Single(_alertas);
        Assert.Equal(TipoAlerta.DesviacionConsumoCarga, alerta.Tipo);
        Assert.Equal(25, alerta.MagnitudDesviacion);
        Assert.Equal(_evento.Id, alerta.EventoId);
        Assert.Equal(_evento.VehiculoId, alerta.VehiculoId);
        Assert.Equal(1, r.AlertasGeneradas);
        Assert.Equal(TipoAlerta.DesviacionConsumoCarga, r.Alertas[0].Tipo);
        Assert.Equal(EstadoAutorizacion.Ejecutada, r.EstadoAutorizacion); // 40 de 40: no es parcial
    }

    [Fact]
    public async Task Ejecucion_parcial_deja_la_autorizacion_ejecutada_parcial_con_alerta()
    {
        _evento.GalonesCargados = 30m;

        var r = await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        Assert.Equal(EstadoAutorizacion.EjecutadaParcial, r.EstadoAutorizacion);
        Assert.Equal(EstadoAutorizacion.EjecutadaParcial, _autorizacion.Estado);
        var alerta = Assert.Single(_alertas);
        Assert.Equal(TipoAlerta.EjecucionParcial, alerta.Tipo);
        Assert.Equal(10, alerta.MagnitudDesviacion);
    }

    [Fact]
    public async Task Estacion_cara_compara_con_el_precio_minimo_de_la_ruta()
    {
        _estacion.PrecioGalonActual = 9800m;
        _eventos.Setup(e => e.ObtenerPrecioMinimoDeRutaAsync("Bogota - Medellin", It.IsAny<CancellationToken>())).ReturnsAsync(9500m);

        await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        var alerta = Assert.Single(_alertas);
        Assert.Equal(TipoAlerta.EstacionCara, alerta.Tipo);
        Assert.Equal(300, alerta.MagnitudDesviacion);
    }

    [Fact]
    public async Task Sin_ruta_en_la_estacion_no_se_busca_referencia()
    {
        _estacion.Ruta = null;
        _estacion.PrecioGalonActual = 9800m;

        await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        Assert.Empty(_alertas);
        _eventos.Verify(e => e.ObtenerPrecioMinimoDeRutaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_precio_en_la_estacion_no_se_busca_referencia()
    {
        _estacion.PrecioGalonActual = null;

        await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        Assert.Empty(_alertas);
        _eventos.Verify(e => e.ObtenerPrecioMinimoDeRutaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sin_referencia_de_la_ruta_no_hay_alerta_de_estacion_cara()
    {
        _estacion.PrecioGalonActual = 9800m;
        _eventos.Setup(e => e.ObtenerPrecioMinimoDeRutaAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((decimal?)null);

        await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        Assert.Empty(_alertas);
    }

    [Fact]
    public async Task Los_umbrales_vienen_de_los_parametros_de_negocio()
    {
        // Tolerancia 0.5: 40 x 0.5 = 20; cargar 25 ya no es parcial.
        _parametros.ToleranciaAutonomiaDefault = 0.5;
        _evento.GalonesCargados = 25m;

        var r = await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        Assert.Equal(EstadoAutorizacion.Ejecutada, r.EstadoAutorizacion);
        Assert.Empty(_alertas);
    }

    [Fact]
    public async Task Audita_con_el_diff_de_estado_y_los_tipos_de_alerta()
    {
        _evento.GalonesCargados = 30m;

        await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        _auditoria.Verify(a => a.RegistrarAsync(
            USUARIO, "verificar_carga", "eventos_tanqueo", _evento.Id,
            It.Is<object?>(d => clsDatosPrueba.Json(d) ==
                "{\"estado\":[\"autorizada\",\"ejecutada_parcial\"],\"estado_autorizacion\":\"ejecutada_parcial\",\"alertas\":[\"ejecucion_parcial\"]}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_verificacion_automatica_se_audita_sin_actor()
    {
        _usuario.SetupGet(u => u.UsuarioId).Returns((Guid?)null);

        await Handler().Handle(new clsVerificarEventoCommand(_evento.Id));

        _auditoria.Verify(a => a.RegistrarAsync(
            null, "verificar_carga", "eventos_tanqueo", _evento.Id, It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.EjecutadaParcial)]
    public async Task Una_autorizacion_ya_verificada_lanza_409_y_no_duplica_alertas(EstadoAutorizacion estado)
    {
        _autorizacion.Estado = estado;

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsVerificarEventoCommand(_evento.Id)));

        Assert.Equal("EVENTO_YA_VERIFICADO", ex.Code);
        Assert.Empty(_alertas);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Expirada)]
    [InlineData(EstadoAutorizacion.Rechazada)]
    public async Task Una_autorizacion_que_no_esta_autorizada_lanza_409_TRANSICION_INVALIDA(EstadoAutorizacion estado)
    {
        _autorizacion.Estado = estado;

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsVerificarEventoCommand(_evento.Id)));

        Assert.Equal("TRANSICION_INVALIDA", ex.Code);
        Assert.Empty(_alertas);
        Assert.Equal(estado, _autorizacion.Estado);
    }

    [Fact]
    public async Task Un_conflicto_de_concurrencia_al_guardar_se_propaga()
    {
        // Dos verificaciones en paralelo: la segunda choca con el rowversion.
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("CONFLICTO_CONCURRENCIA", "otra"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsVerificarEventoCommand(_evento.Id)));

        Assert.Equal("CONFLICTO_CONCURRENCIA", ex.Code);
    }

    [Fact]
    public async Task Evento_inexistente_lanza_404()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsVerificarEventoCommand(Guid.NewGuid())));

        Assert.Equal("EVENTO_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Autorizacion_inexistente_lanza_404()
    {
        _autorizaciones.Setup(a => a.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAutorizacionTanqueo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsVerificarEventoCommand(_evento.Id)));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Estacion_inexistente_lanza_404()
    {
        _contexto.Setup(c => c.ObtenerEstacionAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsEstacion?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsVerificarEventoCommand(_evento.Id)));

        Assert.Equal("ESTACION_NO_ENCONTRADA", ex.Code);
    }
}
