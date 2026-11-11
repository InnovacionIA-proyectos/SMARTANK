using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Geo;

namespace Smartank.Domain.Tests.Autorizaciones;

/// <summary>
/// Reglas de la recomendacion sobre una ruta sintetica en el ecuador: de (0,0) a (0,2) = 222.39 km. Todo se mide en "km sobre la
/// ruta" (1 grado de longitud = 111.19 km), asi los valores esperados se calculan a mano.
/// </summary>
public class clsRecomendacionReglasTests
{
    private const double KM_POR_GRADO = 111.19492664455873;
    private const double LONGITUD_RUTA_KM = 2 * KM_POR_GRADO;

    private static readonly IReadOnlyList<clsCoordenada> _ruta = [new(0, 0), new(0, 2)];

    /// <summary>Punto sobre la ruta a <paramref name="km"/> del origen.</summary>
    private static clsCoordenada EnRuta(double km) => new(0, km / KM_POR_GRADO);

    /// <summary>Estacion a <paramref name="km"/> del origen, desplazada <paramref name="latGrados"/> hacia el norte (fuera de la via).</summary>
    private static clsEstacionCandidata Est(double km, decimal precio, double latGrados = 0) =>
        new(Guid.NewGuid(), latGrados, km / KM_POR_GRADO, precio);

    // El vehiculo esta en el km 50 de la ruta: le faltan 172.39 km.
    private static readonly clsCoordenada _vehiculo = EnRuta(50);

    // ---- Seleccionar: requiere tanqueo -------------------------------------------------------------------------------

    [Fact]
    public void Con_autonomia_que_cubre_la_ruta_restante_no_requiere_tanqueo()
    {
        var e1 = Est(100, 9500);
        var e2 = Est(150, 9300);

        var r = clsRecomendacionReglas.Seleccionar([e1, e2], _ruta, _vehiculo, autonomiaKm: 300);

        Assert.False(r.RequiereTanqueo);
        Assert.True(r.SobreRuta);
        Assert.Equal(LONGITUD_RUTA_KM - 50, r.RutaRestanteKm!.Value, 1);
        // La propuesta se genera igual (para abrir el panel): hay estaciones consideradas.
        Assert.Equal(2, r.Consideradas.Count);
    }

    [Fact]
    public void Con_autonomia_menor_a_la_ruta_restante_requiere_tanqueo()
    {
        var r = clsRecomendacionReglas.Seleccionar([Est(100, 9500)], _ruta, _vehiculo, autonomiaKm: 100);

        Assert.True(r.RequiereTanqueo);
        Assert.True(r.SobreRuta);
    }

    [Fact]
    public void Requiere_tanqueo_solo_si_la_autonomia_es_menor_que_la_ruta_restante()
    {
        // Margen de 10 m a cada lado: evita depender del ultimo decimal de la geometria.
        var restante = LONGITUD_RUTA_KM - 50;

        var justoAlcanza = clsRecomendacionReglas.Seleccionar([Est(100, 9500)], _ruta, _vehiculo, restante + 0.01);
        var justoNoAlcanza = clsRecomendacionReglas.Seleccionar([Est(100, 9500)], _ruta, _vehiculo, restante - 0.01);

        Assert.False(justoAlcanza.RequiereTanqueo);
        Assert.True(justoNoAlcanza.RequiereTanqueo);
    }

    // ---- Seleccionar: tres niveles de respaldo sobre la ruta ----------------------------------------------------------

    [Fact]
    public void Prefiere_las_estaciones_de_la_zona_de_repostaje()
    {
        // Autonomia 100: zona = por delante entre 60 y 100 km. Delante del vehiculo: 50, 70, 90 y 120 km.
        var fueraAntes = Est(100, 8000);    // adelante 50 (barata pero antes de la zona)
        var zona1 = Est(120, 9500);         // adelante 70
        var zona2 = Est(140, 9400);         // adelante 90
        var fueraDespues = Est(170, 7000);  // adelante 120 (mas alla de la autonomia)

        var r = clsRecomendacionReglas.Seleccionar([fueraAntes, zona1, zona2, fueraDespues], _ruta, _vehiculo, 100);

        Assert.Equal([zona1, zona2], r.Consideradas);
        Assert.True(r.SobreRuta);
    }

    [Fact]
    public void Los_limites_de_la_zona_son_el_60_por_ciento_y_el_100_por_ciento_de_la_autonomia()
    {
        // Autonomia 100, vehiculo en el km 50: la zona es de 60 a 100 km por delante (margen de 10 m a cada lado).
        var justoAntes = Est(109.99, 9000);   // adelante 59.99: fuera
        var enMinimo = Est(110.01, 9500);     // adelante 60.01: dentro
        var enMaximo = Est(149.99, 9400);     // adelante 99.99: dentro
        var justoDespues = Est(150.01, 9000); // adelante 100.01: fuera

        var r = clsRecomendacionReglas.Seleccionar([justoAntes, enMinimo, enMaximo, justoDespues], _ruta, _vehiculo, 100);

        Assert.Equal([enMinimo, enMaximo], r.Consideradas);
    }

    [Fact]
    public void Sin_estaciones_en_la_zona_usa_cualquiera_alcanzable_hacia_adelante()
    {
        var cerca1 = Est(70, 9500);   // adelante 20 (< zona)
        var cerca2 = Est(100, 9400);  // adelante 50 (< zona)
        var lejos = Est(190, 8000);   // adelante 140 (fuera de la autonomia)

        var r = clsRecomendacionReglas.Seleccionar([cerca1, cerca2, lejos], _ruta, _vehiculo, 100);

        Assert.Equal([cerca1, cerca2], r.Consideradas);
        Assert.True(r.SobreRuta);
    }

    [Fact]
    public void Si_ninguna_cae_en_la_autonomia_toma_la_mas_cercana_hacia_adelante()
    {
        // Autonomia 20: ninguna a menos de 20 km por delante.
        var masCercana = Est(100, 9600);  // adelante 50
        var otra = Est(150, 7000);        // adelante 100 (mas barata, pero mas lejos)

        var r = clsRecomendacionReglas.Seleccionar([masCercana, otra], _ruta, _vehiculo, 20);

        Assert.Equal([masCercana], r.Consideradas);
        Assert.True(r.SobreRuta);
    }

    [Fact]
    public void La_mas_cercana_conserva_todas_las_empatadas()
    {
        var a = Est(100, 9600);
        var b = Est(100, 9500);

        var r = clsRecomendacionReglas.Seleccionar([a, b], _ruta, _vehiculo, 20);

        Assert.Equal(2, r.Consideradas.Count);
    }

    [Fact]
    public void Nunca_considera_estaciones_detras_del_vehiculo()
    {
        var detras = Est(20, 5000);     // el vehiculo ya la paso
        var adelante = Est(120, 9500);

        var r = clsRecomendacionReglas.Seleccionar([detras, adelante], _ruta, _vehiculo, 100);

        Assert.Equal([adelante], r.Consideradas);
    }

    [Fact]
    public void Una_estacion_justo_en_la_posicion_del_vehiculo_no_cuenta_como_adelante()
    {
        // adelante = 0: la condicion es adelante > 0.
        var aqui = Est(50, 5000);
        var adelante = Est(120, 9500);

        var r = clsRecomendacionReglas.Seleccionar([aqui, adelante], _ruta, _vehiculo, 100);

        Assert.Equal([adelante], r.Consideradas);
    }

    // ---- Seleccionar: umbral de 200 m a la polilinea -----------------------------------------------------------------

    [Fact]
    public void Una_estacion_a_menos_de_200_m_de_la_ruta_cuenta_como_sobre_la_ruta()
    {
        var cerca = Est(120, 9500, latGrados: 0.0017); // 0.189 km de la via

        var r = clsRecomendacionReglas.Seleccionar([cerca], _ruta, _vehiculo, 100);

        Assert.True(r.SobreRuta);
        Assert.Equal([cerca], r.Consideradas);
    }

    [Fact]
    public void Una_estacion_a_mas_de_200_m_de_la_ruta_no_cuenta_y_cae_a_linea_recta()
    {
        var lejos = Est(120, 9500, latGrados: 0.0019); // 0.211 km de la via

        var r = clsRecomendacionReglas.Seleccionar([lejos], _ruta, _vehiculo, 100);

        Assert.False(r.SobreRuta);
        Assert.Equal([lejos], r.Consideradas); // linea recta: la unica, alcanzable (menos de 100 km)
        Assert.NotNull(r.RutaRestanteKm);      // la ruta restante igual se midio
    }

    // ---- Seleccionar: respaldo en linea recta ------------------------------------------------------------------------

    [Fact]
    public void Si_ninguna_esta_sobre_la_ruta_usa_las_alcanzables_en_linea_recta()
    {
        // Una a ~24 km del vehiculo (alcanzable), otra a ~122 km (no alcanzable con 100).
        var alcanzable = Est(60, 9500, latGrados: 0.2);
        var inalcanzable = Est(100, 8000, latGrados: 1.0);

        var r = clsRecomendacionReglas.Seleccionar([alcanzable, inalcanzable], _ruta, _vehiculo, 100);

        Assert.False(r.SobreRuta);
        Assert.Equal([alcanzable], r.Consideradas);
        Assert.True(r.RequiereTanqueo);
    }

    [Fact]
    public void Si_ninguna_es_alcanzable_en_linea_recta_devuelve_todas()
    {
        var a = Est(100, 9500, latGrados: 1.0);
        var b = Est(120, 9400, latGrados: 1.2);

        var r = clsRecomendacionReglas.Seleccionar([a, b], _ruta, _vehiculo, 50);

        Assert.False(r.SobreRuta);
        Assert.Equal(2, r.Consideradas.Count);
    }

    [Fact]
    public void Sin_ruta_propone_tanqueo_y_mide_en_linea_recta()
    {
        var cerca = Est(60, 9500, latGrados: 0.2);
        var lejos = Est(100, 8000, latGrados: 1.0);

        var r = clsRecomendacionReglas.Seleccionar([cerca, lejos], polilinea: null, _vehiculo, 100);

        Assert.True(r.RequiereTanqueo);   // sin ruta no se sabe si alcanza: requiere = true
        Assert.False(r.SobreRuta);
        Assert.Null(r.RutaRestanteKm);
        Assert.Equal([cerca], r.Consideradas);
    }

    [Fact]
    public void Una_polilinea_de_un_solo_punto_equivale_a_no_tener_ruta()
    {
        var e = Est(60, 9500, latGrados: 0.2);

        var r = clsRecomendacionReglas.Seleccionar([e], [new clsCoordenada(0, 0)], _vehiculo, 100);

        Assert.Null(r.RutaRestanteKm);
        Assert.False(r.SobreRuta);
    }

    [Fact]
    public void Sin_posicion_del_vehiculo_propone_tanqueo_con_todas_las_estaciones()
    {
        var a = Est(60, 9500);
        var b = Est(100, 8000, latGrados: 1.0);

        var r = clsRecomendacionReglas.Seleccionar([a, b], _ruta, posicionVehiculo: null, autonomiaKm: 100);

        Assert.True(r.RequiereTanqueo);
        Assert.False(r.SobreRuta);
        Assert.Null(r.RutaRestanteKm);
        Assert.Equal(2, r.Consideradas.Count);
    }

    [Fact]
    public void AlcanzablesEnLineaRecta_sin_posicion_devuelve_todas()
    {
        var todas = new[] { Est(60, 9500), Est(100, 9400) };

        Assert.Equal(todas, clsRecomendacionReglas.AlcanzablesEnLineaRecta(todas, null, 10));
    }

    // ---- ElegirMasBarata / ahorro ------------------------------------------------------------------------------------

    [Fact]
    public void ElegirMasBarata_devuelve_la_mas_barata_y_la_mas_cara()
    {
        var a = Est(100, 9500);
        var b = Est(110, 9100);
        var c = Est(120, 9900);

        var (elegida, masCara) = clsRecomendacionReglas.ElegirMasBarata([a, b, c]);

        Assert.Equal(b, elegida);
        Assert.Equal(c, masCara);
    }

    [Fact]
    public void ElegirMasBarata_en_empate_toma_la_primera()
    {
        var a = Est(100, 9100);
        var b = Est(110, 9100);

        var (elegida, _) = clsRecomendacionReglas.ElegirMasBarata([a, b]);

        Assert.Equal(a, elegida);
    }

    [Fact]
    public void ElegirMasBarata_con_una_sola_estacion_la_devuelve_como_elegida_y_mas_cara()
    {
        var a = Est(100, 9100);

        var (elegida, masCara) = clsRecomendacionReglas.ElegirMasBarata([a]);

        Assert.Equal(a, elegida);
        Assert.Equal(a, masCara);
    }

    [Fact]
    public void CalcularAhorro_es_la_diferencia_de_precio_por_los_galones()
    {
        // (9800 - 9300) x 40 = 20000
        Assert.Equal(20000m, clsRecomendacionReglas.CalcularAhorro(9800m, 9300m, 40m));
    }

    [Fact]
    public void CalcularAhorro_redondea_a_pesos_enteros_mitad_hacia_arriba()
    {
        // 100.5 x 1 = 100.5 -> 101
        Assert.Equal(101m, clsRecomendacionReglas.CalcularAhorro(1100.5m, 1000m, 1m));
    }

    [Fact]
    public void CalcularAhorro_con_una_sola_estacion_es_cero()
    {
        Assert.Equal(0m, clsRecomendacionReglas.CalcularAhorro(9300m, 9300m, 40m));
    }

    // ---- CalcularGalones ---------------------------------------------------------------------------------------------

    private static clsVehiculoParaGalones Vehiculo(
        double? historico = 5, double? esperado = null, double tolerancia = 0.02, decimal? capacidad = null, decimal? nivel = null) =>
        new(historico, esperado, tolerancia, capacidad, nivel);

    [Fact]
    public void Galones_solicitados_mandan_sobre_el_calculo()
    {
        var g = clsRecomendacionReglas.CalcularGalones(25m, 300, 100, Vehiculo(), 8, 40);

        Assert.Equal(25m, g);
    }

    [Fact]
    public void Sin_ruta_restante_conocida_usa_el_default()
    {
        var g = clsRecomendacionReglas.CalcularGalones(null, null, 100, Vehiculo(), 8, 40);

        Assert.Equal(40m, g);
    }

    [Fact]
    public void Sin_vehiculo_usa_el_default()
    {
        var g = clsRecomendacionReglas.CalcularGalones(null, 300, 100, null, 8, 40);

        Assert.Equal(40m, g);
    }

    [Fact]
    public void Galones_cubren_el_deficit_con_el_rendimiento_efectivo()
    {
        // Deficit = 300 - 100 = 200 km; rendimiento efectivo = 5 x 0.98 = 4.9; 200 / 4.9 = 40.8 -> techo 41.
        var g = clsRecomendacionReglas.CalcularGalones(null, 300, 100, Vehiculo(historico: 5, tolerancia: 0.02), 8, 40);

        Assert.Equal(41m, g);
    }

    [Fact]
    public void Galones_se_topan_al_espacio_libre_del_tanque()
    {
        // Pediria 41, pero capacidad 150 - nivel 120 = 30 libres.
        var g = clsRecomendacionReglas.CalcularGalones(
            null, 300, 100, Vehiculo(capacidad: 150, nivel: 120), 8, 40);

        Assert.Equal(30m, g);
    }

    [Fact]
    public void El_espacio_libre_se_redondea_a_galones_enteros()
    {
        // 150 - 119.5 = 30.5 -> 31 (mitad hacia arriba); el deficit pide 41.
        var g = clsRecomendacionReglas.CalcularGalones(
            null, 300, 100, Vehiculo(capacidad: 150, nivel: 119.5m), 8, 40);

        Assert.Equal(31m, g);
    }

    [Fact]
    public void Con_el_tanque_lleno_propone_el_minimo_de_1_galon()
    {
        var g = clsRecomendacionReglas.CalcularGalones(
            null, 300, 100, Vehiculo(capacidad: 150, nivel: 150), 8, 40);

        Assert.Equal(1m, g);
    }

    [Fact]
    public void Si_la_autonomia_cubre_la_ruta_propone_el_minimo_de_1_galon()
    {
        // Deficit 0: la base exige galones > 0, el panel deshabilita "Autorizar".
        var g = clsRecomendacionReglas.CalcularGalones(null, 100, 300, Vehiculo(), 8, 40);

        Assert.Equal(1m, g);
    }

    [Fact]
    public void Sin_historico_usa_el_esperado_y_sin_ninguno_el_default_de_negocio()
    {
        // Esperado 4: rend ef = 4 x 0.98 = 3.92; 196 / 3.92 = 50 -> 50.
        var conEsperado = clsRecomendacionReglas.CalcularGalones(
            null, 296, 100, Vehiculo(historico: null, esperado: 4), 8, 40);
        // Default 8: rend ef = 7.84; 196 / 7.84 = 25 -> 25.
        var conDefault = clsRecomendacionReglas.CalcularGalones(
            null, 296, 100, Vehiculo(historico: null, esperado: null), 8, 40);

        Assert.Equal(50m, conEsperado);
        Assert.Equal(25m, conDefault);
    }

    [Fact]
    public void Con_rendimiento_efectivo_cero_usa_el_default_de_galones()
    {
        var g = clsRecomendacionReglas.CalcularGalones(null, 300, 100, Vehiculo(historico: 0), 8, 40);

        Assert.Equal(40m, g);
    }

    [Fact]
    public void Con_tolerancia_total_el_rendimiento_efectivo_es_cero_y_usa_el_default_de_galones()
    {
        var g = clsRecomendacionReglas.CalcularGalones(null, 300, 100, Vehiculo(tolerancia: 1), 8, 40);

        Assert.Equal(40m, g);
    }

    // ---- Motivo ------------------------------------------------------------------------------------------------------

    [Fact]
    public void Motivo_es_null_cuando_hace_falta_tanquear()
    {
        Assert.Null(clsRecomendacionReglas.MotivoSinTanqueo(true, 100, 150));
    }

    [Fact]
    public void Motivo_menciona_autonomia_y_ruta_restante_redondeadas()
    {
        Assert.Equal(
            "La autonomía (300 km) cubre la ruta restante (172 km).",
            clsRecomendacionReglas.MotivoSinTanqueo(false, 299.6, 172.39));
    }

    [Fact]
    public void Motivo_sin_ruta_restante_es_generico()
    {
        Assert.Equal("La autonomía cubre la ruta.", clsRecomendacionReglas.MotivoSinTanqueo(false, 300, null));
    }

    [Fact]
    public void Constantes_del_origen()
    {
        Assert.Equal(0.2, clsRecomendacionReglas.UMBRAL_RUTA_KM);
        Assert.Equal(0.6, clsRecomendacionReglas.FACTOR_ZONA_AUTONOMIA);
        Assert.Equal(12, clsRecomendacionReglas.VIGENCIA_HORAS);
    }
}
