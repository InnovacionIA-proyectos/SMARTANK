using Smartank.Domain.Geo;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsSeleccionEdsTests
{
    // Ruta recta de 1 grado de longitud sobre el paralelo 4 N (~111 km): de (4,-74) a (4,-73).
    private static readonly clsCoordenada[] RUTA = [new(4.0, -74.0), new(4.0, -73.0)];

    [Fact]
    public void Solo_entran_las_EDS_a_menos_del_umbral_y_se_toma_el_minimo_y_el_maximo_de_sus_precios()
    {
        var eds = new[]
        {
            new clsEdsCandidata(4.02, -73.5, 10_000m),    // 2.2 km al norte de la ruta: cerca
            new clsEdsCandidata(4.03, -73.99, 9_500m),    // 3.3 km al norte, junto al inicio: cerca
            new clsEdsCandidata(4.10, -73.5, 8_000m),     // 11 km: lejos
            new clsEdsCandidata(4.00, -72.9, 7_000m),     // pasado el final de la ruta (~11 km): lejos
        };

        var r = clsSeleccionEds.Calcular(RUTA, eds, umbralKm: 5);

        Assert.Equal(2, r.EdsCerca);
        Assert.Equal(9_500m, r.AcpmMin);
        Assert.Equal(10_000m, r.AcpmMax);
    }

    [Fact]
    public void La_distancia_se_mide_contra_el_segmento_no_contra_el_vertice_mas_cercano()
    {
        // La EDS esta a ~1.1 km del punto medio de un unico segmento de 111 km: su vertice mas cercano queda a ~55 km.
        var eds = new[] { new clsEdsCandidata(4.01, -73.5, 9_000m) };

        var r = clsSeleccionEds.Calcular(RUTA, eds, umbralKm: 5);

        Assert.Equal(1, r.EdsCerca);
        Assert.Equal(9_000m, r.AcpmMin);
        Assert.Equal(9_000m, r.AcpmMax);
    }

    [Fact]
    public void El_umbral_decide_cuales_entran()
    {
        var eds = new[] { new clsEdsCandidata(4.02, -73.5, 10_000m) };   // ~2.2 km

        Assert.Equal(1, clsSeleccionEds.Calcular(RUTA, eds, 3).EdsCerca);
        Assert.Equal(0, clsSeleccionEds.Calcular(RUTA, eds, 2).EdsCerca);
    }

    [Fact]
    public void Sin_EDS_cercanas_el_minimo_y_el_maximo_son_nulos_y_el_conteo_cero()
    {
        var eds = new[] { new clsEdsCandidata(5.0, -70.0, 9_000m) };

        var r = clsSeleccionEds.Calcular(RUTA, eds, 5);

        Assert.Null(r.AcpmMin);
        Assert.Null(r.AcpmMax);
        Assert.Equal(0, r.EdsCerca);
    }

    [Fact]
    public void Una_ruta_de_menos_de_dos_puntos_o_sin_estaciones_no_tiene_referencia()
    {
        var eds = new[] { new clsEdsCandidata(4.0, -73.5, 9_000m) };

        Assert.Equal(0, clsSeleccionEds.Calcular([new clsCoordenada(4, -74)], eds, 5).EdsCerca);
        Assert.Equal(0, clsSeleccionEds.Calcular(RUTA, [], 5).EdsCerca);
    }

    [Fact]
    public void Una_EDS_exactamente_sobre_la_ruta_cuenta_y_el_precio_igual_da_min_y_max_iguales()
    {
        var eds = new[]
        {
            new clsEdsCandidata(4.0, -73.7, 9_900m),
            new clsEdsCandidata(4.0, -73.2, 9_900m),
        };

        var r = clsSeleccionEds.Calcular(RUTA, eds, 5);

        Assert.Equal(2, r.EdsCerca);
        Assert.Equal(9_900m, r.AcpmMin);
        Assert.Equal(9_900m, r.AcpmMax);
    }
}
