using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsSincronizarRutasTests
{
    private readonly Mock<IViajesSincronizacionRepository> _viajes = new();
    private readonly Mock<IRutasLegadoPort> _legado = new();
    private readonly Mock<IRutasSincronizacionRepository> _rutas = new();
    private readonly List<clsRutaUpsertDto> _escritas = [];
    private clsSyncOptions _opciones = new();

    public clsSincronizarRutasTests()
    {
        _rutas.Setup(r => r.UpsertAsync(It.IsAny<IReadOnlyList<clsRutaUpsertDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsRutaUpsertDto>, CancellationToken>((l, _) => _escritas.AddRange(l))
            .ReturnsAsync((IReadOnlyList<clsRutaUpsertDto> l, CancellationToken _) => new clsResultadoUpsertDto(l.Count, 0, []));
        _legado.Setup(l => l.ListarCentrosDaneAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, clsCentroDaneDto>());
    }

    private clsSincronizarRutasCommandHandler Handler() => new(
        _viajes.Object, _legado.Object, _rutas.Object, Options.Create(_opciones), NullLogger<clsSincronizarRutasCommandHandler>.Instance);

    private void ConViajes(params clsViajeParaRutaDto[] viajes) =>
        _viajes.Setup(v => v.ListarParaRutasAsync(It.IsAny<CancellationToken>())).ReturnsAsync(viajes);

    private void ConIndice(params (string Fo, long Id)[] indice) =>
        _legado.Setup(l => l.ListarIndiceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(indice.Select(i => new clsRutaIndiceLegadoDto(i.Fo, i.Id)).ToList());

    private void ConGeometrias(params clsRutaValhallaDto[] g) =>
        _legado.Setup(l => l.ObtenerGeometriasAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(g);

    private static clsViajeParaRutaDto Viaje(string fo, long? almacenado = null, bool tiene = false, string? o = null, string? d = null) =>
        new(Guid.NewGuid(), fo, o, d, almacenado, tiene);

    private static clsRutaValhallaDto Ruta(long id, string fo, string? wkt = "LINESTRING (-74 4, -73.5 4, -73 4)") =>
        new(id, fo, 123.45, 3, wkt, "{\"tramos\":[1]}", new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Una_ruta_de_Valhalla_se_guarda_simplificada_con_fuente_valhalla_y_enlazada_al_viaje()
    {
        var viaje = Viaje("301");
        ConViajes(viaje);
        ConIndice(("301", 55));
        ConGeometrias(Ruta(55, "301"));

        var r = await Handler().Handle(new clsSincronizarRutasCommand());

        var ruta = _escritas.Single();
        Assert.Equal(FuenteRuta.Valhalla, ruta.Fuente);
        Assert.Equal(55, ruta.ValhallaId);
        Assert.Equal(viaje.ViajeId, ruta.ViajeId);
        Assert.Equal("[[4,-74],[4,-73]]", ruta.PolilineaJson);      // el punto medio es colineal y se elimina
        Assert.Equal(2, ruta.NumPuntos);
        Assert.Equal(3, ruta.NumPuntosOriginal);
        Assert.Equal("{\"tramos\":[1]}", ruta.DetalleKmJson);
        Assert.Equal(1, r.Insertados);
    }

    [Fact]
    public async Task La_tolerancia_configurada_decide_cuanto_se_simplifica()
    {
        ConViajes(Viaje("301"));
        ConIndice(("301", 55));
        ConGeometrias(Ruta(55, "301", "LINESTRING (-74 4, -73.5 4.01, -73 4)"));

        _opciones = new clsSyncOptions { RutaReduceTolMetros = 30 };
        await Handler().Handle(new clsSincronizarRutasCommand());
        Assert.Equal(3, _escritas.Single().NumPuntos);

        _escritas.Clear();
        _opciones = new clsSyncOptions { RutaReduceTolMetros = 5_000 };
        await Handler().Handle(new clsSincronizarRutasCommand());
        Assert.Equal(2, _escritas.Single().NumPuntos);
    }

    [Fact]
    public async Task Sin_ruta_en_Valhalla_y_sin_fallback_el_viaje_queda_sin_ruta()
    {
        ConViajes(Viaje("999", o: "000000011001", d: "000000005001"));
        ConIndice();

        var r = await Handler().Handle(new clsSincronizarRutasCommand());

        Assert.Empty(_escritas);
        Assert.Equal(1, r.Extras["sinRuta"]);
        _rutas.Verify(x => x.UpsertAsync(It.IsAny<IReadOnlyList<clsRutaUpsertDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Con_el_fallback_Dane_activo_se_traza_una_recta_entre_los_centros_poblados()
    {
        _opciones = new clsSyncOptions { RutaDaneFallback = true };
        var viaje = Viaje("999", o: "11001", d: "5001");    // se normalizan a 12 digitos
        ConViajes(viaje);
        ConIndice();
        _legado.Setup(l => l.ListarCentrosDaneAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new Dictionary<string, clsCentroDaneDto>
            {
                ["000000011001"] = new("000000011001", 4.711, -74.0721),
                ["000000005001"] = new("000000005001", 6.2442, -75.5812),
            });

        var r = await Handler().Handle(new clsSincronizarRutasCommand());

        var ruta = _escritas.Single();
        Assert.Equal(FuenteRuta.Dane, ruta.Fuente);
        Assert.Null(ruta.ValhallaId);
        Assert.Equal(viaje.ViajeId, ruta.ViajeId);
        Assert.Equal("[[4.711,-74.0721],[6.2442,-75.5812]]", ruta.PolilineaJson);
        Assert.Equal(2, ruta.NumPuntos);
        Assert.InRange(ruta.Kilometros!.Value, 150, 250);
        Assert.Equal(1, r.Extras["dane"]);
    }

    [Fact]
    public async Task El_fallback_Dane_no_pisa_una_ruta_existente_ni_crea_ruta_si_falta_un_centro()
    {
        _opciones = new clsSyncOptions { RutaDaneFallback = true };
        ConViajes(Viaje("1", tiene: true, o: "11001", d: "5001"), Viaje("2", o: "11001", d: "77777"));
        ConIndice();
        _legado.Setup(l => l.ListarCentrosDaneAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new Dictionary<string, clsCentroDaneDto> { ["000000011001"] = new("000000011001", 4.7, -74.0) });

        await Handler().Handle(new clsSincronizarRutasCommand());

        Assert.Empty(_escritas);
    }

    [Fact]
    public async Task El_modo_incremental_salta_los_viajes_cuya_ruta_ya_esta_al_dia()
    {
        ConViajes(Viaje("1", almacenado: 10, tiene: true), Viaje("2", almacenado: 20, tiene: true), Viaje("3"));
        ConIndice(("1", 10), ("2", 21), ("3", 30));
        ConGeometrias(Ruta(21, "2"), Ruta(30, "3"));

        var r = await Handler().Handle(new clsSincronizarRutasCommand(Incremental: true));

        Assert.Equal(1, r.Omitidos);
        Assert.Equal(1, r.Extras["sinCambios"]);
        Assert.Equal([21L, 30L], _escritas.Select(e => e.ValhallaId!.Value).OrderBy(x => x));
        _legado.Verify(l => l.ObtenerGeometriasAsync(
            It.Is<IReadOnlyCollection<long>>(ids => ids.Count == 2 && !ids.Contains(10)), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task El_modo_completo_reprocesa_tambien_las_rutas_que_estan_al_dia()
    {
        ConViajes(Viaje("1", almacenado: 10, tiene: true));
        ConIndice(("1", 10));
        ConGeometrias(Ruta(10, "1"));

        var r = await Handler().Handle(new clsSincronizarRutasCommand(Incremental: false));

        Assert.Equal(0, r.Omitidos);
        Assert.Single(_escritas);
    }

    [Fact]
    public async Task Varios_viajes_con_el_mismo_FO_generan_una_sola_ruta_enlazada_al_que_ya_la_tiene()
    {
        var conRuta = Viaje("0007", almacenado: 5, tiene: true);
        var sinRuta = Viaje("7");
        ConViajes(sinRuta, conRuta);
        ConIndice(("7", 6));
        ConGeometrias(Ruta(6, "7"));

        await Handler().Handle(new clsSincronizarRutasCommand());

        var ruta = _escritas.Single();
        Assert.Equal(conRuta.ViajeId, ruta.ViajeId);
    }

    [Fact]
    public async Task Un_fallo_al_leer_un_lote_se_registra_como_errores_y_no_aborta_los_demas()
    {
        ConViajes(Enumerable.Range(1, 60).Select(i => Viaje(i.ToString())).ToArray());
        ConIndice(Enumerable.Range(1, 60).Select(i => (i.ToString(), (long)i)).ToArray());
        var llamada = 0;
        _legado.Setup(l => l.ObtenerGeometriasAsync(It.IsAny<IReadOnlyCollection<long>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<long> ids, CancellationToken _) =>
            {
                if (++llamada == 1) throw new InvalidOperationException("timeout del legado");
                return ids.Select(id => Ruta(id, id.ToString())).ToList();
            });

        var r = await Handler().Handle(new clsSincronizarRutasCommand());

        Assert.Equal(50, r.ErroresTotal);        // el primer lote (50 FO) falla entero
        Assert.Equal(10, r.Insertados);          // el segundo lote (10 FO) se procesa
        Assert.Contains("timeout", r.Errores[0].Mensaje);
    }

    [Fact]
    public async Task Un_detalle_que_no_es_JSON_se_guarda_como_NULL_para_no_violar_el_CHECK_de_la_base()
    {
        ConViajes(Viaje("1"));
        ConIndice(("1", 1));
        ConGeometrias(Ruta(1, "1") with { DetalleJson = "no es json" });

        await Handler().Handle(new clsSincronizarRutasCommand());

        Assert.Null(_escritas.Single().DetalleKmJson);
    }
}
