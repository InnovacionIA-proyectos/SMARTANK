using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsSincronizarRendimientoTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private const double LITROS_100_GAL = 378.5411784;
    private static readonly Guid V1 = Guid.NewGuid();
    private static readonly Guid V2 = Guid.NewGuid();
    private static readonly Guid V3 = Guid.NewGuid();

    private readonly Mock<IGeotabPort> _geotab = new();
    private readonly Mock<IVehiculosSincronizacionRepository> _vehiculos = new();
    private IReadOnlyList<clsRendimientoVehiculoDto> _escritos = [];
    private DateTimeOffset? _desde;

    public clsSincronizarRendimientoTests()
    {
        _geotab.Setup(g => g.ListarDispositivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new clsGeotabDispositivoDto("d1", "ABC123", null),
            new clsGeotabDispositivoDto("d2", "DEF456", null),
            new clsGeotabDispositivoDto("d3", "GHI789", null),
            new clsGeotabDispositivoDto("d4", "NOEXISTE", null),
        });
        _vehiculos.Setup(v => v.ObtenerMapaPlacaAIdAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["ABC123"] = V1, ["DEF456"] = V2, ["GHI789"] = V3 });
        _vehiculos.Setup(v => v.ActualizarRendimientoAsync(It.IsAny<IReadOnlyList<clsRendimientoVehiculoDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsRendimientoVehiculoDto>, CancellationToken>((l, _) => _escritos = l)
            .ReturnsAsync((IReadOnlyList<clsRendimientoVehiculoDto> l, CancellationToken _) => new clsResultadoUpsertDto(0, l.Count, []));
    }

    private clsSincronizarRendimientoCommandHandler Handler(clsSyncOptions? o = null) => new(
        _geotab.Object, _vehiculos.Object, Options.Create(o ?? new clsSyncOptions()), new clsRelojFalso(AHORA),
        NullLogger<clsSincronizarRendimientoCommandHandler>.Instance);

    private void Lecturas(clsGeotabLecturaDto[] combustible, clsGeotabLecturaDto[] odometro)
    {
        _geotab.Setup(g => g.ObtenerCombustibleUsadoAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((d, _) => _desde = d)
            .ReturnsAsync(combustible);
        _geotab.Setup(g => g.ObtenerOdometroAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(odometro);
    }

    [Fact]
    public async Task Calcula_km_por_galon_con_la_diferencia_de_los_contadores_acumulados()
    {
        // d1: 100 galones y 800 km -> 8.00 km/gal. d2: 100 galones y 833 km -> 8.33.
        Lecturas(
            [
                new("d1", 5_000, AHORA.AddDays(-30)), new("d1", 5_000 + LITROS_100_GAL, AHORA.AddDays(-1)),
                new("d2", 0, AHORA.AddDays(-30)), new("d2", LITROS_100_GAL, AHORA.AddDays(-1)),
            ],
            [
                new("d1", 1_000_000, AHORA.AddDays(-30)), new("d1", 1_800_000, AHORA.AddDays(-1)),
                new("d2", 0, AHORA.AddDays(-30)), new("d2", 833_000, AHORA.AddDays(-1)),
            ]);

        var r = await Handler().Handle(new clsSincronizarRendimientoCommand());

        Assert.Equal(8.0, _escritos.Single(e => e.VehiculoId == V1).KmPorGalon);
        Assert.Equal(8.33, _escritos.Single(e => e.VehiculoId == V2).KmPorGalon);
        Assert.Equal(2, r.Actualizados);
    }

    [Fact]
    public async Task Clasifica_sin_datos_fuera_de_banda_y_sin_vehiculo_sin_escribirlos()
    {
        Lecturas(
            [
                new("d1", 0, AHORA.AddDays(-10)), new("d1", 113.5, AHORA),                          // 29.98 gal: insuficiente
                new("d2", 0, AHORA.AddDays(-10)), new("d2", 200 * 3.785411784, AHORA),              // 200 gal
                new("d3", 0, AHORA.AddDays(-10)), new("d3", LITROS_100_GAL, AHORA),                 // sin odometro
                new("d4", 0, AHORA.AddDays(-10)), new("d4", LITROS_100_GAL, AHORA),                 // placa fuera de la flota
            ],
            [
                new("d1", 0, AHORA.AddDays(-10)), new("d1", 500_000, AHORA),
                new("d2", 0, AHORA.AddDays(-10)), new("d2", 150_000, AHORA),                        // 150 km / 200 gal = 0.75
                new("d4", 0, AHORA.AddDays(-10)), new("d4", 800_000, AHORA),
            ]);

        var r = await Handler().Handle(new clsSincronizarRendimientoCommand());

        Assert.Empty(_escritos);
        Assert.Equal(2, r.Extras["sinDatos"]);       // d1 y d3
        Assert.Equal(1, r.Extras["fueraDeBanda"]);   // d2
        Assert.Equal(1, r.Extras["sinVehiculo"]);    // d4
        Assert.Equal(4, r.Omitidos);
    }

    [Fact]
    public async Task La_ventana_en_dias_viene_de_la_configuracion_o_del_comando()
    {
        Lecturas([], []);

        await Handler(new clsSyncOptions { RendimientoVentanaDias = 30 }).Handle(new clsSincronizarRendimientoCommand());
        Assert.Equal(AHORA.AddDays(-30), _desde);

        await Handler().Handle(new clsSincronizarRendimientoCommand(VentanaDias: 7));
        Assert.Equal(AHORA.AddDays(-7), _desde);

        await Handler().Handle(new clsSincronizarRendimientoCommand());
        Assert.Equal(AHORA.AddDays(-60), _desde);   // defecto
    }

    [Fact]
    public async Task Las_lecturas_desordenadas_usan_la_fecha_y_no_el_orden_de_llegada()
    {
        Lecturas(
            [new("d1", 5_000 + LITROS_100_GAL, AHORA.AddDays(-1)), new("d1", 5_000, AHORA.AddDays(-30))],
            [new("d1", 1_800_000, AHORA.AddDays(-1)), new("d1", 1_000_000, AHORA.AddDays(-30))]);

        await Handler().Handle(new clsSincronizarRendimientoCommand());

        Assert.Equal(8.0, _escritos.Single().KmPorGalon);
    }
}
