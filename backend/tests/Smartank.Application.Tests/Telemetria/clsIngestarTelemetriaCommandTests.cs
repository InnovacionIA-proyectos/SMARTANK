using Moq;
using Smartank.Application.CasosDeUso.Telemetria;
using Smartank.Application.CasosDeUso.Telemetria.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Application.Tests.Telemetria;

public class clsIngestarTelemetriaCommandTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITelemetriaRepository> _vehiculos = new();

    private clsIngestarTelemetriaCommandHandler Handler() => new(_vehiculos.Object, new RelojFijo(AHORA));

    private static clsVehiculo Vehiculo(decimal nivel = 45m) => new()
    {
        Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"),
        Placa = "SNU123",
        TipoMotor = "diesel",
        NivelTanqueActual = nivel,
        Odometro = 152800,
        ToleranciaMotor = 0.02,
    };

    private void VehiculoExiste() =>
        _vehiculos.Setup(v => v.ActualizarTelemetriaAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Vehiculo());

    [Fact]
    public async Task Lectura_valida_actualiza_el_vehiculo_y_responde_ok_con_el_vehiculo_completo()
    {
        VehiculoExiste();

        var respuesta = await Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", 45m, 152800, 4.6, -74.1, null));

        Assert.True(respuesta.Ok);
        Assert.Equal("SNU123", respuesta.Vehiculo.Placa);
        Assert.Equal(45m, respuesta.Vehiculo.NivelTanqueActual);
        // Sin leido_at, la lectura se fecha con la hora del servidor.
        _vehiculos.Verify(v => v.ActualizarTelemetriaAsync("SNU123", 45m, 152800, 4.6, -74.1, AHORA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task La_placa_se_normaliza_a_mayusculas_sin_espacios()
    {
        VehiculoExiste();

        await Handler().Handle(new clsIngestarTelemetriaCommand("  snu123 ", 10m, null, null, null, null));

        _vehiculos.Verify(v => v.ActualizarTelemetriaAsync("SNU123", 10m, null, null, null, AHORA, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Datos_opcionales_ausentes_se_pasan_como_nulos_para_conservar_los_anteriores()
    {
        VehiculoExiste();

        await Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", 0m, null, null, null, null));

        _vehiculos.Verify(v => v.ActualizarTelemetriaAsync("SNU123", 0m, null, null, null, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Usa_leido_at_del_proveedor_cuando_viene()
    {
        VehiculoExiste();
        var leido = AHORA.AddMinutes(-3);

        await Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", 45m, null, null, null, leido));

        _vehiculos.Verify(v => v.ActualizarTelemetriaAsync("SNU123", 45m, null, null, null, leido, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Placa_inexistente_lanza_VEHICULO_NO_ENCONTRADO()
    {
        _vehiculos.Setup(v => v.ActualizarTelemetriaAsync(
                It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<double?>(), It.IsAny<double?>(), It.IsAny<double?>(),
                It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsVehiculo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsIngestarTelemetriaCommand("XYZ999", 45m, null, null, null, null)));

        Assert.Equal("VEHICULO_NO_ENCONTRADO", ex.Code);
        Assert.Contains("XYZ999", ex.Message);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("ABCDEF")]
    [InlineData("AB1234")]
    [InlineData("ABC1")]
    [InlineData("ABC1234")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Placa_con_formato_invalido_lanza_TELEMETRIA_INVALIDA(string? placa)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsIngestarTelemetriaCommand(placa, 10m, null, null, null, null)));

        Assert.Equal("TELEMETRIA_INVALIDA", ex.Code);
        _vehiculos.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("ABC123")]
    [InlineData("ABC12")]
    [InlineData("ABC12D")]
    [InlineData("abc123")]
    public async Task Placas_validas_de_los_formatos_colombianos_se_aceptan(string placa)
    {
        VehiculoExiste();

        var respuesta = await Handler().Handle(new clsIngestarTelemetriaCommand(placa, 10m, null, null, null, null));

        Assert.True(respuesta.Ok);
    }

    [Fact]
    public async Task Falta_nivel_galones_lanza_TELEMETRIA_INVALIDA()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", null, null, null, null, null)));

        Assert.Equal("TELEMETRIA_INVALIDA", ex.Code);
        Assert.Contains("nivel_galones", ex.Message);
    }

    [Theory]
    [InlineData(-0.5, null, null, null, "nivel_galones")]
    [InlineData(10, -1.0, null, null, "odometro")]
    [InlineData(10, null, 90.5, null, "lat")]
    [InlineData(10, null, -91.0, null, "lat")]
    [InlineData(10, null, null, 180.5, "lng")]
    [InlineData(10, null, null, -181.0, "lng")]
    public async Task Valores_fuera_de_rango_lanzan_TELEMETRIA_INVALIDA_indicando_el_campo(
        double nivel, double? odometro, double? lat, double? lng, string campo)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", (decimal)nivel, odometro, lat, lng, null)));

        Assert.Equal("TELEMETRIA_INVALIDA", ex.Code);
        Assert.Contains(campo, ex.Message);
    }

    [Fact]
    public async Task Nivel_absurdo_que_desbordaria_la_columna_se_rechaza()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", 99_999_999m, null, null, null, null)));

        Assert.Equal("TELEMETRIA_INVALIDA", ex.Code);
    }

    [Fact]
    public async Task Los_limites_exactos_de_lat_y_lng_son_validos()
    {
        VehiculoExiste();

        var respuesta = await Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", 1m, 0, 90, 180, null));

        Assert.True(respuesta.Ok);
    }

    [Fact]
    public async Task Lectura_con_fecha_futura_se_rechaza()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", 10m, null, null, null, AHORA.AddHours(2))));

        Assert.Equal("TELEMETRIA_INVALIDA", ex.Code);
        Assert.Contains("leido_at", ex.Message);
    }

    [Fact]
    public async Task Una_pequena_diferencia_de_reloj_con_el_proveedor_se_tolera()
    {
        VehiculoExiste();

        var respuesta = await Handler().Handle(new clsIngestarTelemetriaCommand("SNU123", 10m, null, null, null, AHORA.AddMinutes(4)));

        Assert.True(respuesta.Ok);
    }

    [Fact]
    public async Task Varios_errores_se_reportan_juntos_en_los_detalles()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsIngestarTelemetriaCommand("12", -5m, null, 100, null, null)));

        var errores = Assert.IsAssignableFrom<IEnumerable<Smartank.Application.CasosDeUso.Telemetria.Validadores.clsErrorCampo>>(ex.Details);
        Assert.Equal(new[] { "placa", "nivel_galones", "lat" }, errores.Select(e => e.Campo));
    }

    /// <summary>Reloj de prueba con hora fija.</summary>
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
