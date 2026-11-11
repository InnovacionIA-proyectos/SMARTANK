using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsSincronizarTelemetriaTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid V1 = Guid.NewGuid();
    private static readonly Guid V2 = Guid.NewGuid();

    private readonly Mock<IGeotabPort> _geotab = new();
    private readonly Mock<IVehiculosSincronizacionRepository> _vehiculos = new();
    private IReadOnlyList<clsActualizacionTelemetriaDto> _escritos = [];
    private DateTimeOffset? _desdeNivel;

    public clsSincronizarTelemetriaTests()
    {
        _geotab.Setup(g => g.ListarDispositivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new clsGeotabDispositivoDto("b1", "abc123 ", null),
            new clsGeotabDispositivoDto("b2", null, "XYZ789"),
            new clsGeotabDispositivoDto("b3", "SINFLOTA", null),
        });
        _geotab.Setup(g => g.ObtenerEstadoDispositivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<clsGeotabEstadoDto>());
        _geotab.Setup(g => g.ObtenerNivelCombustibleAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .Callback<DateTimeOffset, CancellationToken>((d, _) => _desdeNivel = d)
            .ReturnsAsync(Array.Empty<clsGeotabLecturaDto>());
        _geotab.Setup(g => g.ObtenerOdometroAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<clsGeotabLecturaDto>());
        _vehiculos.Setup(v => v.ListarBasicosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new clsVehiculoBasicoDto(V1, "ABC123", 150m),
            new clsVehiculoBasicoDto(V2, "XYZ789", null),
        });
        _vehiculos.Setup(v => v.ActualizarTelemetriaAsync(It.IsAny<IReadOnlyList<clsActualizacionTelemetriaDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsActualizacionTelemetriaDto>, CancellationToken>((l, _) => _escritos = l)
            .ReturnsAsync((IReadOnlyList<clsActualizacionTelemetriaDto> l, CancellationToken _) => new clsResultadoUpsertDto(0, l.Count, []));
    }

    private clsSincronizarTelemetriaCommandHandler Handler(clsSyncOptions? o = null) => new(
        _geotab.Object, _vehiculos.Object, Options.Create(o ?? new clsSyncOptions()), new clsRelojFalso(AHORA),
        NullLogger<clsSincronizarTelemetriaCommandHandler>.Instance);

    private void ConNivel(params clsGeotabLecturaDto[] l) =>
        _geotab.Setup(g => g.ObtenerNivelCombustibleAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(l);

    [Fact]
    public async Task El_nivel_en_porcentaje_se_convierte_a_galones_con_la_capacidad_del_tanque()
    {
        ConNivel(new clsGeotabLecturaDto("b1", 62.5, AHORA.AddMinutes(-5)));

        var r = await Handler().Handle(new clsSincronizarTelemetriaCommand());

        var t = _escritos.Single();
        Assert.Equal(V1, t.VehiculoId);
        Assert.Equal(93.75m, t.NivelGalones);          // 0.625 x 150
        Assert.Equal(AHORA.AddMinutes(-5), t.NivelAt);
        Assert.Equal("b1", t.GeotabDeviceId);
        Assert.Equal(1, r.Extras["conNivel"]);
    }

    [Fact]
    public async Task De_varias_lecturas_del_mismo_dispositivo_gana_la_mas_reciente_por_fecha()
    {
        ConNivel(
            new clsGeotabLecturaDto("b1", 80, AHORA.AddHours(-1)),
            new clsGeotabLecturaDto("b1", 40, AHORA.AddMinutes(-1)),
            new clsGeotabLecturaDto("b1", 10, AHORA.AddHours(-3)));

        await Handler().Handle(new clsSincronizarTelemetriaCommand());

        Assert.Equal(60m, _escritos.Single().NivelGalones);   // 40 % de 150
    }

    [Fact]
    public async Task Sin_capacidad_de_tanque_no_se_calcula_el_nivel()
    {
        ConNivel(new clsGeotabLecturaDto("b2", 50, AHORA));

        await Handler().Handle(new clsSincronizarTelemetriaCommand());

        Assert.Empty(_escritos);
    }

    [Fact]
    public async Task La_ventana_de_nivel_configurada_define_desde_cuando_se_consulta()
    {
        await Handler(new clsSyncOptions { VentanaNivelHoras = 24 }).Handle(new clsSincronizarTelemetriaCommand());

        Assert.Equal(AHORA.AddHours(-24), _desdeNivel);
    }

    [Fact]
    public async Task La_ubicacion_ignora_coordenadas_cero_o_fuera_de_rango_y_conserva_la_valida()
    {
        _geotab.Setup(g => g.ObtenerEstadoDispositivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new clsGeotabEstadoDto("b1", 4.711, -74.07, AHORA.AddMinutes(-2)),
            new clsGeotabEstadoDto("b2", 0, 0, AHORA),
            new clsGeotabEstadoDto("b3", 95, 10, AHORA),
        });

        var r = await Handler().Handle(new clsSincronizarTelemetriaCommand());

        var t = _escritos.Single();
        Assert.Equal(4.711, t.Lat);
        Assert.Equal(-74.07, t.Lng);
        Assert.Null(t.NivelGalones);                 // sin lectura de nivel: nulo para que la base conserve el anterior
        Assert.Equal(1, r.Extras["conUbicacion"]);
    }

    [Fact]
    public async Task Los_dispositivos_cuya_placa_no_esta_en_la_flota_se_cuentan_como_omitidos()
    {
        _geotab.Setup(g => g.ObtenerEstadoDispositivosAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new clsGeotabEstadoDto("b3", 4.7, -74.0, AHORA),
        });

        var r = await Handler().Handle(new clsSincronizarTelemetriaCommand());

        Assert.Empty(_escritos);
        Assert.Equal(1, r.Omitidos);
    }

    [Fact]
    public async Task El_odometro_se_convierte_de_metros_a_km()
    {
        ConNivel(new clsGeotabLecturaDto("b1", 50, AHORA));
        _geotab.Setup(g => g.ObtenerOdometroAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new clsGeotabLecturaDto("b1", 100_000, AHORA.AddHours(-5)),
            new clsGeotabLecturaDto("b1", 123_456, AHORA.AddMinutes(-1)),
        });

        await Handler().Handle(new clsSincronizarTelemetriaCommand());

        Assert.Equal(123.456, _escritos.Single().OdometroKm!.Value, 6);
    }

    [Fact]
    public async Task Si_falla_solo_el_odometro_se_sincroniza_igual_la_posicion_y_el_nivel()
    {
        ConNivel(new clsGeotabLecturaDto("b1", 50, AHORA));
        _geotab.Setup(g => g.ObtenerOdometroAsync(It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("500"));

        await Handler().Handle(new clsSincronizarTelemetriaCommand());

        var t = _escritos.Single();
        Assert.Equal(75m, t.NivelGalones);
        Assert.Null(t.OdometroKm);
    }

    [Fact]
    public async Task El_error_de_una_fila_se_reporta_con_la_placa_y_no_con_el_id()
    {
        ConNivel(new clsGeotabLecturaDto("b1", 50, AHORA));
        _vehiculos.Setup(v => v.ActualizarTelemetriaAsync(It.IsAny<IReadOnlyList<clsActualizacionTelemetriaDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsResultadoUpsertDto(0, 0, [new clsErrorSincronizacion(V1.ToString(), "check")]));

        var r = await Handler().Handle(new clsSincronizarTelemetriaCommand());

        Assert.Equal("ABC123", r.Errores.Single().Clave);
        Assert.Equal(0, r.Extras["conNivel"]);
    }
}
