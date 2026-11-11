using Smartank.Domain.Geo;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsRutaPolilineaTests
{
    [Fact]
    public void Valhalla_simplifica_puntos_colineales_conservando_solo_los_extremos()
    {
        // WKT en orden (lon lat): cinco puntos sobre el paralelo 4.
        var puntos = clsRutaPolilinea.DesdeValhalla("LINESTRING (-74 4, -73.75 4, -73.5 4, -73.25 4, -73 4)", 30);

        Assert.Equal([new clsCoordenada(4, -74), new clsCoordenada(4, -73)], puntos);
    }

    [Fact]
    public void Valhalla_conserva_un_desvio_mayor_que_la_tolerancia_y_respeta_el_orden()
    {
        // El punto central sube ~0.01 grados (1.1 km): con 30 m de tolerancia se conserva.
        var puntos = clsRutaPolilinea.DesdeValhalla("LINESTRING (-74 4, -73.5 4.01, -73 4)", 30);

        Assert.Equal(3, puntos.Count);
        Assert.Equal(new clsCoordenada(4, -74), puntos[0]);
        Assert.Equal(new clsCoordenada(4.01, -73.5), puntos[1]);
        Assert.Equal(new clsCoordenada(4, -73), puntos[2]);
    }

    [Fact]
    public void Una_tolerancia_grande_aplana_el_desvio()
    {
        var puntos = clsRutaPolilinea.DesdeValhalla("LINESTRING (-74 4, -73.5 4.01, -73 4)", 5_000);

        Assert.Equal(2, puntos.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LINESTRING EMPTY")]
    public void Un_WKT_sin_coordenadas_da_polilinea_vacia(string? wkt)
    {
        Assert.Empty(clsRutaPolilinea.DesdeValhalla(wkt, 30));
    }

    [Fact]
    public void Dane_es_una_recta_de_dos_puntos_origen_destino()
    {
        var origen = new clsCoordenada(4.711, -74.0721);
        var destino = new clsCoordenada(6.2442, -75.5812);

        var puntos = clsRutaPolilinea.DesdeDane(origen, destino);

        Assert.Equal([origen, destino], puntos);
    }

    [Fact]
    public void Los_kilometros_de_una_ruta_Dane_son_la_distancia_entre_los_dos_centros_con_dos_decimales()
    {
        var puntos = clsRutaPolilinea.DesdeDane(new clsCoordenada(4, -74), new clsCoordenada(4, -73));

        var km = clsRutaPolilinea.Kilometros(puntos);

        Assert.InRange(km, 110.5, 111.5);
        Assert.Equal(Math.Round(km, 2), km);
    }

    [Fact]
    public void El_JSON_de_la_polilinea_es_lat_lng_con_punto_decimal_y_sin_espacios()
    {
        var json = clsRutaPolilinea.AJson([new clsCoordenada(4, -74), new clsCoordenada(4.5, -73.25)]);

        Assert.Equal("[[4,-74],[4.5,-73.25]]", json);
    }

    [Fact]
    public void Una_polilinea_vacia_es_un_arreglo_JSON_valido()
    {
        Assert.Equal("[]", clsRutaPolilinea.AJson([]));
    }
}
