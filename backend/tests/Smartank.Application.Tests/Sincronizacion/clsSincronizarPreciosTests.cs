using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Enums;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsSincronizarPreciosTests
{
    private readonly Mock<IPreciosEdsPort> _fuentes = new();
    private readonly Mock<IPreciosNegociadosPort> _negociados = new();
    private readonly Mock<IEstacionesSincronizacionRepository> _estaciones = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly List<clsActualizacionPrecioEstacionDto> _actualizadas = [];
    private readonly List<clsEstacionNuevaDto> _nuevas = [];

    private static readonly clsEstacionReferencia EXISTENTE_DISTRACOM = new(Guid.NewGuid(), "Distracom Allegro", 4.7, -74.0, "55");
    private static readonly clsEstacionReferencia EXISTENTE_TERPEL = new(Guid.NewGuid(), "Terpel Norte", 4.7110, -74.0721, null);

    public clsSincronizarPreciosTests()
    {
        _fuentes.Setup(f => f.ObtenerTerpelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<clsPrecioEdsDto>());
        _fuentes.Setup(f => f.ObtenerDistracomAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<clsPrecioEdsDto>());
        _negociados.Setup(n => n.ObtenerAcpmPorNombre()).Returns(new Dictionary<string, decimal>());
        _estaciones.Setup(e => e.ListarPorProveedorAsync("distracom", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { EXISTENTE_DISTRACOM });
        _estaciones.Setup(e => e.ListarPorProveedorAsync("terpel", It.IsAny<CancellationToken>())).ReturnsAsync(new[] { EXISTENTE_TERPEL });
        _estaciones.Setup(e => e.ActualizarPreciosAsync(It.IsAny<IReadOnlyList<clsActualizacionPrecioEstacionDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsActualizacionPrecioEstacionDto>, CancellationToken>((l, _) => _actualizadas.AddRange(l))
            .ReturnsAsync((IReadOnlyList<clsActualizacionPrecioEstacionDto> l, CancellationToken _) => new clsResultadoUpsertDto(0, l.Count, []));
        _estaciones.Setup(e => e.InsertarAsync(It.IsAny<IReadOnlyList<clsEstacionNuevaDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsEstacionNuevaDto>, CancellationToken>((l, _) => _nuevas.AddRange(l))
            .ReturnsAsync((IReadOnlyList<clsEstacionNuevaDto> l, CancellationToken _) => new clsResultadoUpsertDto(l.Count, 0, []));
    }

    private clsSincronizarPreciosCommandHandler Handler() => new(
        _fuentes.Object, _negociados.Object, _estaciones.Object, _auditoria.Object,
        NullLogger<clsSincronizarPreciosCommandHandler>.Instance);

    private static clsPrecioEdsDto Eds(FuenteTanqueo p, string? codigo, string nombre, double? lat, double? lng, decimal? acpm, decimal? corriente) =>
        new(p, codigo, nombre, lat, lng, acpm, corriente);

    [Fact]
    public async Task Distracom_actualiza_la_EDS_existente_por_codigo_y_crea_la_que_no_esta()
    {
        _fuentes.Setup(f => f.ObtenerDistracomAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Eds(FuenteTanqueo.Distracom, "55", "Distracom Allegro", 4.7, -74.0, 10_499m, 12_000m),
            Eds(FuenteTanqueo.Distracom, "77", "Distracom Nueva", 5.0, -75.0, 10_000m, null),
        });

        var r = await Handler().Handle(new clsSincronizarPreciosCommand());

        Assert.Equal("automatico", r.Modo);
        var act = Assert.Single(_actualizadas);
        Assert.Equal(EXISTENTE_DISTRACOM.Id, act.EstacionId);
        Assert.Equal(10_499m, act.Acpm);
        Assert.Equal(12_000m, act.Corriente);
        var nueva = Assert.Single(_nuevas);
        Assert.Equal("Distracom Nueva", nueva.Nombre);
        Assert.Equal("distracom", nueva.Proveedor);
        Assert.Equal("77", nueva.CodigoExterno);
        Assert.Equal("ACPM", nueva.Producto);
        Assert.Equal(new clsResultadoFuentePreciosDto(2, 1, 1), r.Fuente["distracom"]);
    }

    [Fact]
    public async Task Terpel_cruza_por_coordenadas_cercanas_aunque_cambie_el_nombre()
    {
        _fuentes.Setup(f => f.ObtenerTerpelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Eds(FuenteTanqueo.Terpel, null, "EDS TERPEL NORTE RENOMBRADA", 4.7112, -74.0721, 11_000m, 13_000m),
        });

        var r = await Handler().Handle(new clsSincronizarPreciosCommand());

        Assert.Equal(EXISTENTE_TERPEL.Id, Assert.Single(_actualizadas).EstacionId);
        Assert.Empty(_nuevas);
        Assert.Equal(new clsResultadoFuentePreciosDto(1, 1, 0), r.Fuente["terpel"]);
    }

    [Fact]
    public async Task El_ACPM_negociado_prevalece_sobre_el_de_los_mapas_al_actualizar_y_al_crear()
    {
        _negociados.Setup(n => n.ObtenerAcpmPorNombre()).Returns(new Dictionary<string, decimal>
        {
            ["DISTRACOM ALLEGRO"] = 10_000m,
            ["DISTRACOM NUEVA"] = 9_900m,
        });
        _fuentes.Setup(f => f.ObtenerDistracomAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Eds(FuenteTanqueo.Distracom, "55", "Distracom Allegro", 4.7, -74.0, 10_999m, null),
            Eds(FuenteTanqueo.Distracom, "77", "Distracom Nueva", 5.0, -75.0, 10_999m, null),
        });

        await Handler().Handle(new clsSincronizarPreciosCommand());

        Assert.Equal(10_000m, _actualizadas.Single().Acpm);
        Assert.Equal(9_900m, _nuevas.Single().Acpm);
    }

    [Fact]
    public async Task Una_EDS_nueva_sin_nombre_no_se_crea()
    {
        _fuentes.Setup(f => f.ObtenerTerpelAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Eds(FuenteTanqueo.Terpel, null, "  ", 9.0, -70.0, 10_000m, null),
        });

        await Handler().Handle(new clsSincronizarPreciosCommand());

        Assert.Empty(_nuevas);
    }

    [Fact]
    public async Task Si_una_fuente_falla_se_registra_el_error_y_la_otra_se_sincroniza()
    {
        _fuentes.Setup(f => f.ObtenerTerpelAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("Terpel respondio 503."));
        _fuentes.Setup(f => f.ObtenerDistracomAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            Eds(FuenteTanqueo.Distracom, "55", "Distracom Allegro", 4.7, -74.0, 10_499m, null),
        });

        var r = await Handler().Handle(new clsSincronizarPreciosCommand());

        Assert.Equal(1, r.Fuente["distracom"].Actualizadas);
        Assert.Equal(0, r.Fuente["terpel"].Total);
        Assert.Contains(r.Errores, e => e.StartsWith("Terpel:") && e.Contains("503"));
        Assert.True(r.ComoResumen().TieneErrores);
    }

    [Fact]
    public async Task Solo_audita_cuando_se_pide_a_nombre_del_actor()
    {
        var actor = Guid.NewGuid();

        await Handler().Handle(new clsSincronizarPreciosCommand());
        _auditoria.Verify(a => a.RegistrarAsync(It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);

        await Handler().Handle(new clsSincronizarPreciosCommand(actor, Auditar: true));
        _auditoria.Verify(a => a.RegistrarAsync(actor, "sync_precios_auto", "estaciones", null,
            It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_resumen_suma_los_contadores_de_ambas_redes()
    {
        var r = new clsResultadoPreciosDto("automatico", new Dictionary<string, clsResultadoFuentePreciosDto>
        {
            ["distracom"] = new(10, 8, 2),
            ["terpel"] = new(5, 5, 0),
        }, []);

        var resumen = r.ComoResumen();

        Assert.Equal(15, resumen.Total);
        Assert.Equal(2, resumen.Insertados);
        Assert.Equal(13, resumen.Actualizados);
        Assert.False(resumen.TieneErrores);
    }

    // ---------- Carga manual ----------

    private clsActualizarPreciosManualCommandHandler HandlerManual() => new(_estaciones.Object, _auditoria.Object);

    [Fact]
    public async Task La_carga_manual_actualiza_todas_audita_y_devuelve_el_contrato_original()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        _estaciones.Setup(e => e.ObtenerInexistentesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<Guid>());
        _estaciones.Setup(e => e.ActualizarPrecioGalonAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Guid id, decimal p, CancellationToken _) => new clsEstacionPrecioDto(
                id, "X", null, null, null, p, null, null, null, true, null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));

        var r = await HandlerManual().Handle(new clsActualizarPreciosManualCommand(
            Guid.NewGuid(), [new clsPrecioManualItemDto(a, 10_000m), new clsPrecioManualItemDto(b, 11_000m)]));

        Assert.Equal("manual", r.Modo);
        Assert.Equal(2, r.Actualizadas);
        Assert.Equal([10_000m, 11_000m], r.Estaciones.Select(e => e.PrecioGalonActual!.Value));
        _auditoria.Verify(x => x.RegistrarAsync(It.IsAny<Guid?>(), "sync_precios_manual", "estaciones", null,
            It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task La_carga_manual_con_una_estacion_inexistente_no_escribe_ninguna()
    {
        var existe = Guid.NewGuid();
        var falta = Guid.NewGuid();
        _estaciones.Setup(e => e.ObtenerInexistentesAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { falta });

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => HandlerManual().Handle(
            new clsActualizarPreciosManualCommand(null, [new clsPrecioManualItemDto(existe, 1m), new clsPrecioManualItemDto(falta, 2m)])));

        Assert.Equal("ESTACION_NO_ENCONTRADA", ex.Code);
        _estaciones.Verify(e => e.ActualizarPrecioGalonAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task La_carga_manual_rechaza_lista_vacia_y_precios_negativos()
    {
        var vacia = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            HandlerManual().Handle(new clsActualizarPreciosManualCommand(null, [])));
        Assert.Equal("PRECIOS_VACIOS", vacia.Code);

        var negativo = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            HandlerManual().Handle(new clsActualizarPreciosManualCommand(null, [new clsPrecioManualItemDto(Guid.NewGuid(), -1m)])));
        Assert.Equal("PRECIO_INVALIDO", negativo.Code);
    }
}
