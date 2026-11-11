using Smartank.Domain.Geo;

namespace Smartank.Domain.Tests.Geo;

public class clsPolilineaTests
{
    [Fact]
    public void ParseLineStringWkt_lee_pares_lon_lat_y_los_devuelve_como_lat_lng_en_orden()
    {
        var puntos = clsPolilinea.ParseLineStringWkt("LINESTRING (-74.0721 4.711, -75.5812 6.2442, -76.532 3.4516)");

        Assert.Equal(3, puntos.Count);
        Assert.Equal(new clsCoordenada(4.711, -74.0721), puntos[0]);
        Assert.Equal(new clsCoordenada(6.2442, -75.5812), puntos[1]);
        Assert.Equal(new clsCoordenada(3.4516, -76.532), puntos[2]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("LINESTRING EMPTY")]
    public void ParseLineStringWkt_sin_coordenadas_devuelve_lista_vacia(string? wkt)
    {
        Assert.Empty(clsPolilinea.ParseLineStringWkt(wkt));
    }

    [Fact]
    public void Simplificar_con_dos_puntos_o_menos_no_cambia_nada()
    {
        var dos = new[] { new clsCoordenada(0, 0), new clsCoordenada(1, 1) };
        Assert.Equal(dos, clsPolilinea.Simplificar(dos));
        Assert.Empty(clsPolilinea.Simplificar([]));
    }

    [Fact]
    public void Simplificar_elimina_los_puntos_colineales_y_conserva_los_extremos()
    {
        var recta = new[]
        {
            new clsCoordenada(0, 0), new clsCoordenada(0, 1), new clsCoordenada(0, 2), new clsCoordenada(0, 3),
        };

        var simple = clsPolilinea.Simplificar(recta);

        Assert.Equal([new clsCoordenada(0, 0), new clsCoordenada(0, 3)], simple);
    }

    [Fact]
    public void Simplificar_conserva_las_curvas_que_exceden_la_tolerancia_y_el_orden()
    {
        // El punto intermedio se desvia 0.1 grados (~11 km), muy por encima de 30 metros.
        var curva = new[] { new clsCoordenada(0, 0), new clsCoordenada(0.1, 1), new clsCoordenada(0, 2) };

        var simple = clsPolilinea.Simplificar(curva, 30);

        Assert.Equal(curva, simple);
    }

    [Fact]
    public void Simplificar_descarta_la_curva_menor_a_la_tolerancia()
    {
        // Desvio de 0.0001 grados (~11 m) con tolerancia de 30 m: el punto central sobra.
        var casiRecta = new[] { new clsCoordenada(0, 0), new clsCoordenada(0.0001, 1), new clsCoordenada(0, 2) };

        var simple = clsPolilinea.Simplificar(casiRecta, 30);

        Assert.Equal([new clsCoordenada(0, 0), new clsCoordenada(0, 2)], simple);
    }
}
