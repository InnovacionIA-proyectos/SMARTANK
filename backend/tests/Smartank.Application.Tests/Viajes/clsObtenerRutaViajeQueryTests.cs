using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Viajes;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Viajes;

public class clsObtenerRutaViajeQueryTests
{
    private static readonly Guid ID = Guid.Parse("33333333-cccc-cccc-cccc-333333333333");
    private static readonly DateTimeOffset UBICACION = new(2026, 6, 15, 11, 55, 0, TimeSpan.Zero);

    private readonly Mock<IViajesLecturaRepository> _viajes = new();

    private clsObtenerRutaViajeQueryHandler Handler() =>
        new(_viajes.Object, Options.Create(new clsParametrosNegocio { RendimientoDefaultKmGal = 8 }));

    private static clsViajeRutaFila Fila(
        double[][]? polilinea = null, double? lat = 4.6, double? lng = -74.1, decimal? nivel = 40m) => new(
        ID, "Bogota-Villavicencio", EstadoViaje.EnRuta, "SNU123", lat, lng, UBICACION, nivel, 8.5, null, 0.02, 666, 3, polilinea);

    [Fact]
    public async Task Devuelve_polilinea_posicion_y_autonomia()
    {
        double[][] polilinea = [[4.6, -74.1], [4.4, -73.9], [4.15, -73.6]];
        _viajes.Setup(v => v.ObtenerRutaAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync(Fila(polilinea));

        var dto = await Handler().Handle(new clsObtenerRutaViajeQuery(ID));

        Assert.Equal(ID, dto.Viaje.Id);
        Assert.Equal("Bogota-Villavicencio", dto.Viaje.Ruta);
        Assert.Equal(EstadoViaje.EnRuta, dto.Viaje.Estado);
        Assert.Equal("SNU123", dto.Viaje.Placa);
        Assert.NotNull(dto.Ruta);
        Assert.Equal(polilinea, dto.Ruta!.Polilinea);
        Assert.Equal(666, dto.Ruta.Kilometros);
        Assert.Equal(3, dto.Ruta.NumPuntos);
        Assert.NotNull(dto.Vehiculo);
        Assert.Equal("SNU123", dto.Vehiculo!.Placa);
        Assert.Equal(4.6, dto.Vehiculo.Lat);
        Assert.Equal(-74.1, dto.Vehiculo.Lng);
        Assert.Equal(UBICACION, dto.Vehiculo.UbicacionAt);
        // 40 x 8.5 x 0.98 = 333.2 -> 333
        Assert.Equal(333, dto.AutonomiaKm);
    }

    [Fact]
    public async Task Viaje_sin_ruta_calculada_devuelve_ruta_nula()
    {
        _viajes.Setup(v => v.ObtenerRutaAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync(Fila(polilinea: null));

        var dto = await Handler().Handle(new clsObtenerRutaViajeQuery(ID));

        Assert.Null(dto.Ruta);
        Assert.NotNull(dto.Vehiculo);
    }

    [Theory]
    [InlineData(null, -74.1)]
    [InlineData(4.6, null)]
    [InlineData(null, null)]
    public async Task Vehiculo_sin_posicion_completa_devuelve_vehiculo_nulo(double? lat, double? lng)
    {
        _viajes.Setup(v => v.ObtenerRutaAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync(Fila(lat: lat, lng: lng));

        var dto = await Handler().Handle(new clsObtenerRutaViajeQuery(ID));

        Assert.Null(dto.Vehiculo);
    }

    [Fact]
    public async Task Vehiculo_sin_nivel_no_tiene_autonomia()
    {
        _viajes.Setup(v => v.ObtenerRutaAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync(Fila(nivel: null));

        var dto = await Handler().Handle(new clsObtenerRutaViajeQuery(ID));

        Assert.Null(dto.AutonomiaKm);
    }

    [Fact]
    public async Task Viaje_inexistente_lanza_VIAJE_NO_ENCONTRADO()
    {
        _viajes.Setup(v => v.ObtenerRutaAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync((clsViajeRutaFila?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsObtenerRutaViajeQuery(ID)));

        Assert.Equal("VIAJE_NO_ENCONTRADO", ex.Code);
    }
}
