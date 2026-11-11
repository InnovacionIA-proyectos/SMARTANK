using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Geo;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsSincronizarPreciosRutaTests
{
    private readonly Mock<IEstacionesSincronizacionRepository> _estaciones = new();
    private readonly Mock<IPreciosRutaRepository> _repo = new();
    private readonly List<clsPrecioReferenciaViajeDto> _escritos = [];
    private int _lotes;

    private static readonly clsCoordenada[] RUTA = [new(4.0, -74.0), new(4.0, -73.0)];

    public clsSincronizarPreciosRutaTests()
    {
        _repo.Setup(r => r.UpsertAsync(It.IsAny<IReadOnlyList<clsPrecioReferenciaViajeDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsPrecioReferenciaViajeDto>, CancellationToken>((l, _) => { _lotes++; _escritos.AddRange(l); })
            .ReturnsAsync((IReadOnlyList<clsPrecioReferenciaViajeDto> l, CancellationToken _) => new clsResultadoUpsertDto(l.Count, 0, []));
    }

    private clsSincronizarPreciosRutaCommandHandler Handler(double umbral = 5) => new(
        _estaciones.Object, _repo.Object, Options.Create(new clsSyncOptions { RutaEdsUmbralKm = umbral }),
        NullLogger<clsSincronizarPreciosRutaCommandHandler>.Instance);

    private static async IAsyncEnumerable<clsRutaParaPrecioDto> Flujo(IEnumerable<clsRutaParaPrecioDto> rutas)
    {
        foreach (var r in rutas) yield return r;
        await Task.CompletedTask;
    }

    private void Dado(IEnumerable<clsEdsCandidata> eds, IEnumerable<clsRutaParaPrecioDto> rutas)
    {
        _estaciones.Setup(e => e.ListarConPrecioAcpmAsync(It.IsAny<CancellationToken>())).ReturnsAsync(eds.ToList());
        _repo.Setup(r => r.ListarRutasConPolilineaAsync(It.IsAny<CancellationToken>())).Returns(() => Flujo(rutas));
    }

    [Fact]
    public async Task Guarda_minimo_maximo_y_cantidad_de_EDS_cercanas_por_viaje()
    {
        var viaje = Guid.NewGuid();
        Dado(
            [new(4.02, -73.5, 10_000m), new(4.03, -73.99, 9_500m), new(4.5, -73.5, 8_000m)],
            [new clsRutaParaPrecioDto(viaje, RUTA)]);

        var r = await Handler().Handle(new clsSincronizarPreciosRutaCommand());

        var p = _escritos.Single();
        Assert.Equal(viaje, p.ViajeId);
        Assert.Equal(9_500m, p.AcpmMin);
        Assert.Equal(10_000m, p.AcpmMax);
        Assert.Equal(2, p.EdsCerca);
        Assert.Equal(1, r.Extras["conEdsCerca"]);
    }

    [Fact]
    public async Task Un_viaje_sin_EDS_cercanas_igual_queda_registrado_con_cero_y_precios_nulos()
    {
        Dado([new(9.0, -70.0, 9_000m)], [new clsRutaParaPrecioDto(Guid.NewGuid(), RUTA)]);

        var r = await Handler().Handle(new clsSincronizarPreciosRutaCommand());

        var p = _escritos.Single();
        Assert.Equal(0, p.EdsCerca);
        Assert.Null(p.AcpmMin);
        Assert.Null(p.AcpmMax);
        Assert.Equal(0, r.Extras["conEdsCerca"]);
        Assert.Equal(1, r.Total);
    }

    [Fact]
    public async Task El_umbral_configurado_se_aplica()
    {
        var eds = new[] { new clsEdsCandidata(4.04, -73.5, 9_000m) };   // ~4.4 km
        Dado(eds, [new clsRutaParaPrecioDto(Guid.NewGuid(), RUTA)]);

        await Handler(umbral: 5).Handle(new clsSincronizarPreciosRutaCommand());
        Assert.Equal(1, _escritos.Single().EdsCerca);

        _escritos.Clear();
        await Handler(umbral: 3).Handle(new clsSincronizarPreciosRutaCommand());
        Assert.Equal(0, _escritos.Single().EdsCerca);
    }

    [Fact]
    public async Task Las_escrituras_se_hacen_por_lotes_de_500()
    {
        var rutas = Enumerable.Range(0, 1_200).Select(_ => new clsRutaParaPrecioDto(Guid.NewGuid(), RUTA)).ToList();
        Dado([new(4.0, -73.5, 9_000m)], rutas);

        var r = await Handler().Handle(new clsSincronizarPreciosRutaCommand());

        Assert.Equal(3, _lotes);         // 500 + 500 + 200
        Assert.Equal(1_200, _escritos.Count);
        Assert.Equal(1_200, r.Total);
        Assert.Equal(1_200, r.Insertados);
    }

    [Fact]
    public async Task Sin_rutas_no_escribe_nada()
    {
        Dado([new(4.0, -73.5, 9_000m)], []);

        var r = await Handler().Handle(new clsSincronizarPreciosRutaCommand());

        Assert.Equal(0, r.Total);
        _repo.Verify(x => x.UpsertAsync(It.IsAny<IReadOnlyList<clsPrecioReferenciaViajeDto>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Los_errores_de_un_lote_se_acumulan_sin_abortar()
    {
        Dado([new(4.0, -73.5, 9_000m)], Enumerable.Range(0, 600).Select(_ => new clsRutaParaPrecioDto(Guid.NewGuid(), RUTA)));
        var llamada = 0;
        _repo.Setup(x => x.UpsertAsync(It.IsAny<IReadOnlyList<clsPrecioReferenciaViajeDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<clsPrecioReferenciaViajeDto> l, CancellationToken _) =>
                ++llamada == 1
                    ? new clsResultadoUpsertDto(l.Count - 1, 0, [new clsErrorSincronizacion("viaje X", "fk")])
                    : new clsResultadoUpsertDto(l.Count, 0, []));

        var r = await Handler().Handle(new clsSincronizarPreciosRutaCommand());

        Assert.Equal(1, r.ErroresTotal);
        Assert.Equal(599, r.Insertados);
    }
}
