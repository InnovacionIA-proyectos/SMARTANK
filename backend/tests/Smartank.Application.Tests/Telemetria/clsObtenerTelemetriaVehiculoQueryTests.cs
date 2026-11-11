using Moq;
using Smartank.Application.CasosDeUso.Telemetria;
using Smartank.Application.CasosDeUso.Telemetria.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Application.Tests.Telemetria;

public class clsObtenerTelemetriaVehiculoQueryTests
{
    private static readonly Guid ID = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
    private static readonly DateTimeOffset ACTUALIZADO = new(2026, 6, 15, 9, 30, 0, TimeSpan.Zero);

    private readonly Mock<ITelemetriaRepository> _vehiculos = new();

    private clsObtenerTelemetriaVehiculoQueryHandler Handler() => new(_vehiculos.Object);

    [Fact]
    public async Task Devuelve_el_ultimo_estado_de_tanque()
    {
        _vehiculos.Setup(v => v.ObtenerVehiculoAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsVehiculo { Id = ID, Placa = "SNU123", TipoMotor = "diesel", NivelTanqueActual = 40m, Odometro = 152800, UpdatedAt = ACTUALIZADO });

        var dto = await Handler().Handle(new clsObtenerTelemetriaVehiculoQuery(ID));

        Assert.Equal(ID, dto.VehiculoId);
        Assert.Equal("SNU123", dto.Placa);
        Assert.Equal(40m, dto.NivelTanqueActual);
        Assert.Equal(152800, dto.Odometro);
        Assert.True(dto.TieneTelemetria);
        Assert.Equal(ACTUALIZADO, dto.ActualizadoAt);
    }

    [Fact]
    public async Task Vehiculo_que_nunca_reporto_nivel_no_tiene_telemetria()
    {
        _vehiculos.Setup(v => v.ObtenerVehiculoAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsVehiculo { Id = ID, Placa = "WGK456", TipoMotor = "diesel", NivelTanqueActual = null });

        var dto = await Handler().Handle(new clsObtenerTelemetriaVehiculoQuery(ID));

        Assert.False(dto.TieneTelemetria);
        Assert.Null(dto.NivelTanqueActual);
    }

    [Fact]
    public async Task Tanque_en_cero_si_cuenta_como_telemetria()
    {
        _vehiculos.Setup(v => v.ObtenerVehiculoAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync(
            new clsVehiculo { Id = ID, Placa = "WGK456", TipoMotor = "diesel", NivelTanqueActual = 0m });

        var dto = await Handler().Handle(new clsObtenerTelemetriaVehiculoQuery(ID));

        Assert.True(dto.TieneTelemetria);
    }

    [Fact]
    public async Task Vehiculo_inexistente_lanza_VEHICULO_NO_ENCONTRADO()
    {
        _vehiculos.Setup(v => v.ObtenerVehiculoAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync((clsVehiculo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsObtenerTelemetriaVehiculoQuery(ID)));

        Assert.Equal("VEHICULO_NO_ENCONTRADO", ex.Code);
    }
}
