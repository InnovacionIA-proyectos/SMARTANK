using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsReglasPreciosTests
{
    [Theory]
    [InlineData(4000, 4000)]
    [InlineData(10499, 10499)]
    [InlineData(40000, 40000)]
    public void Un_precio_dentro_de_la_banda_realista_es_valido(double precio, double esperado)
    {
        Assert.Equal((decimal)esperado, clsReglasPrecios.PrecioValido(precio));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]            // placeholder de la fuente
    [InlineData(1170)]         // error de origen
    [InlineData(3999.99)]
    [InlineData(40000.01)]
    [InlineData(112450)]       // error de origen
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void Un_precio_fuera_de_la_banda_se_descarta(double precio)
    {
        Assert.Null(clsReglasPrecios.PrecioValido(precio));
    }

    [Fact]
    public void Sin_dato_no_hay_precio()
    {
        Assert.Null(clsReglasPrecios.PrecioValido(null));
    }

    [Theory]
    [InlineData(4.711, -74.0721, true)]
    [InlineData(-4.9, -81.9, true)]
    [InlineData(15.9, -66.1, true)]
    [InlineData(0, 0, false)]
    [InlineData(40.4, -3.7, false)]          // Madrid
    [InlineData(4.711, -60, false)]
    [InlineData(double.NaN, -74, false)]
    public void Solo_son_validas_las_coordenadas_dentro_de_Colombia(double lat, double lng, bool esperado)
    {
        Assert.Equal(esperado, clsReglasPrecios.CoordenadaValida(lat, lng));
    }

    [Theory]
    [InlineData("  distracom   Doña  Juana ", "DISTRACOM DONA JUANA")]
    [InlineData("Distracom El Caimán", "DISTRACOM EL CAIMAN")]
    [InlineData("COMBUSTIBLES DEL PACÍFICO", "COMBUSTIBLES DEL PACIFICO")]
    [InlineData("EDS\tLa  Esperanza", "EDS LA ESPERANZA")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void El_nombre_se_normaliza_a_mayusculas_sin_tildes_y_con_espacios_colapsados(string? nombre, string esperado)
    {
        Assert.Equal(esperado, clsReglasPrecios.NormalizarNombre(nombre));
    }

    [Fact]
    public void El_ACPM_negociado_prevalece_sobre_el_de_la_fuente_aunque_la_fuente_no_tenga_precio()
    {
        var negociados = new Dictionary<string, decimal> { ["DISTRACOM DONA JUANA"] = 10_509m };

        Assert.Equal(10_509m, clsReglasPrecios.ResolverAcpm(negociados, "Distracom Doña Juana", 11_000m));
        Assert.Equal(10_509m, clsReglasPrecios.ResolverAcpm(negociados, "Distracom Doña Juana", null));
    }

    [Fact]
    public void Una_EDS_que_no_esta_en_la_lista_negociada_usa_el_precio_de_la_fuente()
    {
        var negociados = new Dictionary<string, decimal> { ["DISTRACOM DONA JUANA"] = 10_509m };

        Assert.Equal(11_000m, clsReglasPrecios.ResolverAcpm(negociados, "Terpel La Pista", 11_000m));
        Assert.Null(clsReglasPrecios.ResolverAcpm(negociados, "Terpel La Pista", null));
    }

    [Theory]
    [InlineData(10_000, 12_000, "ACPM - Corriente")]
    [InlineData(10_000, null, "ACPM")]
    [InlineData(null, 12_000, "Corriente")]
    [InlineData(null, null, null)]
    public void El_producto_de_una_EDS_nueva_lista_lo_que_vende(int? acpm, int? corriente, string? esperado)
    {
        Assert.Equal(esperado, clsReglasPrecios.DescribirProducto(acpm, corriente));
    }
}
