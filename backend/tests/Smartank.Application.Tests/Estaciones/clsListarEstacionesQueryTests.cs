using Moq;
using Smartank.Application.CasosDeUso.Estaciones;
using Smartank.Application.CasosDeUso.Estaciones.Interfaces;
using Smartank.Domain.Estaciones.Entidades;

namespace Smartank.Application.Tests.Estaciones;

public class clsListarEstacionesQueryTests
{
    private readonly Mock<IEstacionesRepository> _estaciones = new();

    private clsListarEstacionesQueryHandler Handler() => new(_estaciones.Object);

    private static clsEstacion Eds(string nombre, decimal? precio, string? ruta = "bogota-villavicencio") => new()
    {
        Id = Guid.NewGuid(),
        Nombre = nombre,
        Ruta = ruta,
        Lat = 4.65,
        Lng = -74.09,
        PrecioGalonActual = precio,
        PrecioAcpm = precio,
        Proveedor = "terpel",
        Producto = "ACPM",
        CodigoExterno = "EDS0001",
        SoportaPreautorizacion = true,
    };

    [Fact]
    public async Task Lista_con_total_ruta_y_la_forma_completa_de_cada_estacion()
    {
        _estaciones.Setup(e => e.ListarAsync("bogota-villavicencio", It.IsAny<CancellationToken>()))
            .ReturnsAsync([Eds("EDS Primax Centro", 9300m), Eds("EDS Terpel Norte", 10100m)]);

        var respuesta = await Handler().Handle(new clsListarEstacionesQuery("bogota-villavicencio"));

        Assert.Equal("bogota-villavicencio", respuesta.Ruta);
        Assert.Equal(2, respuesta.Total);
        Assert.Equal("EDS Primax Centro", respuesta.Estaciones[0].Nombre);
        Assert.Equal(9300m, respuesta.Estaciones[0].PrecioGalonActual);
        Assert.Equal("EDS0001", respuesta.Estaciones[0].CodigoExterno);
        Assert.True(respuesta.Estaciones[0].SoportaPreautorizacion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Ruta_ausente_o_en_blanco_no_filtra_y_se_devuelve_nula(string? ruta)
    {
        _estaciones.Setup(e => e.ListarAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([Eds("A", 9000m), Eds("B", null, null)]);

        var respuesta = await Handler().Handle(new clsListarEstacionesQuery(ruta));

        Assert.Null(respuesta.Ruta);
        Assert.Equal(2, respuesta.Total);
        _estaciones.Verify(e => e.ListarAsync(null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task La_ruta_se_recorta_antes_de_consultar()
    {
        _estaciones.Setup(e => e.ListarAsync("ruta-x", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var respuesta = await Handler().Handle(new clsListarEstacionesQuery("  ruta-x "));

        Assert.Equal("ruta-x", respuesta.Ruta);
        Assert.Equal(0, respuesta.Total);
        Assert.Empty(respuesta.Estaciones);
    }

    [Fact]
    public async Task Estaciones_sin_precio_se_listan_con_precio_nulo()
    {
        _estaciones.Setup(e => e.ListarAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([Eds("Sin precio", null)]);

        var respuesta = await Handler().Handle(new clsListarEstacionesQuery(null));

        Assert.Null(Assert.Single(respuesta.Estaciones).PrecioGalonActual);
    }
}
