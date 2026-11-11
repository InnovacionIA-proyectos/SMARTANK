using Smartank.Domain.Enums;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsReglasViajeTests
{
    [Theory]
    [InlineData("000000301453730", "301453730")]
    [InlineData("301453730", "301453730")]
    [InlineData("  0012  ", "12")]
    [InlineData("0000", "0000")]      // todo ceros: se conserva el original
    [InlineData("", null)]
    [InlineData(null, null)]
    public void El_manifiesto_es_el_numero_de_FO_sin_ceros_a_la_izquierda(string? numeroFo, string? esperado)
    {
        Assert.Equal(esperado, clsReglasViaje.Manifiesto(numeroFo));
    }

    [Theory]
    [InlineData("BOGOTA - MEDELLIN", "R-01", "BOGOTA - MEDELLIN")]
    [InlineData("", "R-01", "R-01")]
    [InlineData("   ", "R-01", "R-01")]
    [InlineData(null, "R-01", "R-01")]
    [InlineData(null, "  ", "")]
    [InlineData(null, null, "")]
    public void La_ruta_usa_el_nombre_y_si_esta_vacio_el_codigo(string? nombre, string? codigo, string esperado)
    {
        Assert.Equal(esperado, clsReglasViaje.ResolverRuta(nombre, codigo));
    }

    [Fact]
    public void Los_programados_no_tienen_inicio_y_el_resto_usa_la_fecha_de_despacho()
    {
        var despacho = new DateTimeOffset(2026, 5, 1, 13, 0, 0, TimeSpan.Zero);

        Assert.Null(clsReglasViaje.InicioAt(EstadoViaje.Programado, despacho));
        Assert.Equal(despacho, clsReglasViaje.InicioAt(EstadoViaje.EnRuta, despacho));
        Assert.Equal(despacho, clsReglasViaje.InicioAt(EstadoViaje.Cerrado, despacho));
        Assert.Equal(despacho, clsReglasViaje.InicioAt(EstadoViaje.Descartado, despacho));
        Assert.Null(clsReglasViaje.InicioAt(EstadoViaje.EnRuta, null));
    }
}
