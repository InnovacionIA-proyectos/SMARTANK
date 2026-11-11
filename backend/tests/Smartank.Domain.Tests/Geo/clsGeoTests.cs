using Smartank.Domain.Geo;

namespace Smartank.Domain.Tests.Geo;

public class clsGeoTests
{
    // Un grado de latitud sobre la esfera de radio 6371 km mide 6371 * pi/180 = 111.1949 km.
    private const double KM_POR_GRADO = 111.19492664455873;
    private const double TOLERANCIA = 0.01;

    // Polilinea sobre el ecuador de (0,0) a (0,2): 2 grados de longitud = 222.39 km.
    private static readonly IReadOnlyList<clsCoordenada> _ecuador =
    [
        new clsCoordenada(0, 0),
        new clsCoordenada(0, 2),
    ];

    [Fact]
    public void DistanciaKm_entre_el_mismo_punto_es_cero()
    {
        Assert.Equal(0, clsGeo.DistanciaKm(4.711, -74.0721, 4.711, -74.0721), 9);
    }

    [Fact]
    public void DistanciaKm_de_un_grado_de_latitud_mide_111_km()
    {
        Assert.Equal(KM_POR_GRADO, clsGeo.DistanciaKm(0, 0, 1, 0), 3);
    }

    [Fact]
    public void DistanciaKm_Bogota_Medellin_esta_en_el_orden_de_240_km_en_linea_recta()
    {
        var km = clsGeo.DistanciaKm(4.711, -74.0721, 6.2442, -75.5812);
        Assert.InRange(km, 235, 245);
    }

    [Fact]
    public void LongitudPolilineaKm_suma_los_tramos()
    {
        var polilinea = new[] { new clsCoordenada(0, 0), new clsCoordenada(1, 0), new clsCoordenada(2, 0) };
        Assert.Equal(2 * KM_POR_GRADO, clsGeo.LongitudPolilineaKm(polilinea), 3);
    }

    [Fact]
    public void LongitudPolilineaKm_de_menos_de_dos_puntos_es_cero()
    {
        Assert.Equal(0, clsGeo.LongitudPolilineaKm([]));
        Assert.Equal(0, clsGeo.LongitudPolilineaKm([new clsCoordenada(1, 1)]));
    }

    [Fact]
    public void PosicionSobrePolilineaKm_ubica_un_punto_a_mitad_de_la_ruta()
    {
        // Punto 0.01 grados al norte del ecuador, a mitad de la ruta: recorrio 1 grado y esta a ~1.11 km de la via.
        var pos = clsGeo.PosicionSobrePolilineaKm(new clsCoordenada(0.01, 1), _ecuador);

        Assert.Equal(KM_POR_GRADO, pos.PosKm, 1);
        Assert.Equal(0.01 * KM_POR_GRADO, pos.DistKm, 2);
    }

    [Fact]
    public void PosicionSobrePolilineaKm_acota_la_proyeccion_a_los_extremos_del_segmento()
    {
        var antes = clsGeo.PosicionSobrePolilineaKm(new clsCoordenada(0, -1), _ecuador);
        var despues = clsGeo.PosicionSobrePolilineaKm(new clsCoordenada(0, 3), _ecuador);

        Assert.Equal(0, antes.PosKm, 6);
        Assert.Equal(2 * KM_POR_GRADO, despues.PosKm, 3);
    }

    [Fact]
    public void PosicionSobrePolilineaKm_acumula_los_segmentos_previos()
    {
        // Ruta en L: (0,0) -> (0,1) -> (1,1). Un punto sobre el segundo tramo, a 0.5 grados de latitud.
        var ruta = new[] { new clsCoordenada(0, 0), new clsCoordenada(0, 1), new clsCoordenada(1, 1) };
        var pos = clsGeo.PosicionSobrePolilineaKm(new clsCoordenada(0.5, 1), ruta);

        Assert.Equal(1.5 * KM_POR_GRADO, pos.PosKm, 1);
        Assert.True(pos.DistKm < TOLERANCIA);
    }

    [Fact]
    public void PosicionSobrePolilineaKm_con_polilinea_vacia_devuelve_distancia_infinita()
    {
        var pos = clsGeo.PosicionSobrePolilineaKm(new clsCoordenada(1, 1), []);
        Assert.True(double.IsPositiveInfinity(pos.DistKm));
        Assert.Equal(0, pos.PosKm);
    }

    [Fact]
    public void DistanciaAPolilineaKm_mide_la_distancia_perpendicular_minima()
    {
        Assert.Equal(0.1 * KM_POR_GRADO, clsGeo.DistanciaAPolilineaKm(new clsCoordenada(0.1, 1), _ecuador), 2);
    }

    [Fact]
    public void DistanciaAPolilineaKm_con_un_solo_punto_usa_haversine()
    {
        var punto = new clsCoordenada(1, 0);
        var distancia = clsGeo.DistanciaAPolilineaKm(punto, [new clsCoordenada(0, 0)]);
        Assert.Equal(KM_POR_GRADO, distancia, 3);
    }

    [Fact]
    public void ProyectarEnPolilinea_pega_el_punto_a_la_ruta()
    {
        var proyectado = clsGeo.ProyectarEnPolilinea(new clsCoordenada(0.5, 1), _ecuador);

        Assert.Equal(0, proyectado.Lat, 6);
        Assert.Equal(1, proyectado.Lng, 6);
    }

    [Fact]
    public void ProyectarEnPolilinea_sin_ruta_devuelve_el_mismo_punto()
    {
        var punto = new clsCoordenada(3, 4);
        Assert.Equal(punto, clsGeo.ProyectarEnPolilinea(punto, []));
    }

    [Theory]
    [InlineData(0, 0.5, 0.25)]
    [InlineData(0, 3, 1)]
    [InlineData(0, -1, 0)]
    public void ProgresoRuta_es_la_fraccion_acotada_entre_origen_y_destino(double lat, double lng, double esperado)
    {
        var progreso = clsGeo.ProgresoRuta(new clsCoordenada(0, 0), new clsCoordenada(0, 2), new clsCoordenada(lat, lng));
        Assert.Equal(esperado, progreso, 6);
    }

    [Fact]
    public void ProgresoRuta_con_origen_igual_a_destino_es_cero()
    {
        var punto = new clsCoordenada(1, 1);
        Assert.Equal(0, clsGeo.ProgresoRuta(punto, punto, new clsCoordenada(2, 2)));
    }
}
