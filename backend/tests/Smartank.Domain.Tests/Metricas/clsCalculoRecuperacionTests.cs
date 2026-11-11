using Smartank.Domain.Metricas.Reglas;

namespace Smartank.Domain.Tests.Metricas;

/// <summary>Casos con valores calculados a mano (golden): los totales de cada prueba se explican en el comentario.</summary>
public class clsCalculoRecuperacionTests
{
    private static readonly TimeSpan BOGOTA = TimeSpan.FromHours(-5);
    private static readonly Guid EDS_CENTRO = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly clsEstacionCatalogo CENTRO = new(EDS_CENTRO, "EDS Primax Centro", 4.65, -74.09, "distracom", 9100m);
    private static readonly clsExtremosRuta BOG_VVC = new(4.6, -74.1, 4.15, -73.6);

    private static clsTanqueoMetrica Tanqueo(
        string placa, DateTimeOffset fecha, decimal? galones, decimal? precio, decimal? total = null,
        string? proveedor = "terpel", string? estacion = "EDS Primax Centro", string? ciudad = "Bogotá",
        string? ruta = "Bogota-Villavicencio", decimal? min = null, decimal? max = null,
        clsEstacionCatalogo? catalogo = null, clsExtremosRuta? extremos = null) =>
        new(Guid.NewGuid(), proveedor, placa, fecha, galones, precio, total, estacion, ciudad, ruta, min, max, catalogo, extremos);

    /// <summary>
    /// T1: ABC123, 10-jun, 40 gal a 9.300, ruta con min 9.000 y max 10.000, en la EDS Centro.
    /// T2: ABC123, 20-jun, 50 gal a 9.500, misma ruta y referencia, en la EDS Centro (distracom).
    /// T3: XYZ987, 05-jul, 30 gal a 10.100, SIN referencia de ruta (ideal y maximo caen al precio pagado).
    /// </summary>
    private static List<clsTanqueoMetrica> Muestra() =>
    [
        Tanqueo("ABC123", new(2026, 6, 10, 10, 0, 0, BOGOTA), 40m, 9300m, 372000m, "terpel", min: 9000m, max: 10000m, catalogo: CENTRO, extremos: BOG_VVC),
        Tanqueo("ABC123", new(2026, 6, 20, 8, 0, 0, BOGOTA), 50m, 9500m, 475000m, "distracom", min: 9000m, max: 10000m, catalogo: CENTRO, extremos: BOG_VVC),
        Tanqueo("XYZ987", new(2026, 7, 5, 15, 0, 0, BOGOTA), 30m, 10100m, 303000m, "terpel", estacion: "EDS Terpel Norte", ciudad: "Tunja", ruta: null),
    ];

    [Fact]
    public void Totales_con_datos_calculados_a_mano()
    {
        var t = clsCalculoRecuperacion.CalcularTotales(Muestra());

        // galones = 40 + 50 + 30
        Assert.Equal(120m, t.Galones);
        Assert.Equal(3, t.Tanqueos);
        // real   = 40x9300 + 50x9500 + 30x10100 = 372.000 + 475.000 + 303.000
        Assert.Equal(1_150_000m, t.GastoReal);
        // ideal  = 40x9000 + 50x9000 + 30x10100 (sin referencia: su propio precio) = 360.000 + 450.000 + 303.000
        Assert.Equal(1_113_000m, t.GastoIdeal);
        // maximo = 40x10000 + 50x10000 + 30x10100 = 400.000 + 500.000 + 303.000
        Assert.Equal(1_203_000m, t.GastoMaximo);
        // ahorro vs maximo = 1.203.000 - 1.150.000 ; oportunidad vs ideal = 1.150.000 - 1.113.000
        Assert.Equal(53_000m, t.AhorroVsMaximo);
        Assert.Equal(37_000m, t.OportunidadVsIdeal);
        Assert.Equal(9000m, t.PrecioMinimoRuta);
    }

    [Fact]
    public void Sin_tanqueos_todo_es_cero_y_no_hay_precio_minimo()
    {
        var t = clsCalculoRecuperacion.CalcularTotales([]);

        Assert.Equal(0m, t.Galones);
        Assert.Equal(0, t.Tanqueos);
        Assert.Equal(0m, t.GastoReal);
        Assert.Equal(0m, t.AhorroVsMaximo);
        Assert.Equal(0m, t.OportunidadVsIdeal);
        Assert.Null(t.PrecioMinimoRuta);
    }

    [Fact]
    public void Tanqueo_sin_referencia_de_ruta_no_genera_ahorro_ni_oportunidad()
    {
        var t = clsCalculoRecuperacion.CalcularTotales(
            [Tanqueo("XYZ987", new(2026, 7, 5, 0, 0, 0, BOGOTA), 30m, 10100m)]);

        Assert.Equal(303_000m, t.GastoReal);
        Assert.Equal(0m, t.AhorroVsMaximo);
        Assert.Equal(0m, t.OportunidadVsIdeal);
        Assert.Null(t.PrecioMinimoRuta);
    }

    [Fact]
    public void Tanqueo_sin_precio_pagado_cuenta_como_tanqueo_pero_no_suma_gasto_real()
    {
        // Sin precio, galones x precio es nulo y la suma lo ignora (como el SUM de SQL); el ideal si usa el minimo de la ruta.
        var t = clsCalculoRecuperacion.CalcularTotales(
            [Tanqueo("ABC123", new(2026, 6, 1, 0, 0, 0, BOGOTA), 10m, null, 95000m, min: 9000m, max: 10000m)]);

        Assert.Equal(1, t.Tanqueos);
        Assert.Equal(10m, t.Galones);
        Assert.Equal(0m, t.GastoReal);
        Assert.Equal(90_000m, t.GastoIdeal);
        Assert.Equal(100_000m, t.GastoMaximo);
    }

    [Fact]
    public void Utilizacion_agrupa_por_proveedor_de_mayor_a_menor_numero_de_tanqueos()
    {
        var u = clsCalculoRecuperacion.CalcularUtilizacion(Muestra());

        Assert.Equal(2, u.Count);
        Assert.Equal(new clsUtilizacionEds("terpel", 2, 70m), u[0]);      // 40 + 30
        Assert.Equal(new clsUtilizacionEds("distracom", 1, 50m), u[1]);
    }

    [Fact]
    public void Utilizacion_nombra_los_tanqueos_sin_proveedor()
    {
        var u = clsCalculoRecuperacion.CalcularUtilizacion(
            [Tanqueo("ABC123", new(2026, 6, 1, 0, 0, 0, BOGOTA), 5m, 9000m, proveedor: null)]);

        Assert.Equal(clsCalculoRecuperacion.SIN_PROVEEDOR, Assert.Single(u).Proveedor);
    }

    [Fact]
    public void Equipos_toma_el_tanqueo_mas_barato_de_cada_placa_y_mes()
    {
        var equipos = clsCalculoRecuperacion.CalcularEquipos(Muestra());

        Assert.Equal(2, equipos.Count);

        // ABC123 en junio: T1 (9.300) gana a T2 (9.500).
        var abc = equipos[0];
        Assert.Equal("ABC123", abc.Equipo);
        Assert.Equal(6, abc.Mes);
        Assert.Equal(9300m, abc.ValorGalon);
        Assert.Equal(40m, abc.Galones);
        Assert.Equal(372000m, abc.ValorTotal);
        Assert.Equal("EDS Primax Centro", abc.Eds);
        Assert.Equal("Bogotá", abc.Ciudad);

        Assert.Equal("XYZ987", equipos[1].Equipo);
        Assert.Equal(7, equipos[1].Mes);
        Assert.Equal("Tunja", equipos[1].Ciudad);
    }

    [Fact]
    public void Equipos_separa_los_meses_de_una_misma_placa_y_ordena_por_placa_y_mes()
    {
        var tanqueos = new List<clsTanqueoMetrica>
        {
            Tanqueo("ZZZ999", new(2026, 3, 1, 0, 0, 0, BOGOTA), 10m, 9000m),
            Tanqueo("ABC123", new(2026, 5, 1, 0, 0, 0, BOGOTA), 10m, 9100m),
            Tanqueo("ABC123", new(2026, 2, 1, 0, 0, 0, BOGOTA), 10m, 9200m),
        };

        var equipos = clsCalculoRecuperacion.CalcularEquipos(tanqueos);

        Assert.Equal(new[] { ("ABC123", 2), ("ABC123", 5), ("ZZZ999", 3) }, equipos.Select(e => (e.Equipo, e.Mes)));
    }

    [Fact]
    public void Equipos_sin_precio_usa_valor_total_sobre_galones()
    {
        // 190.000 / 20 = 9.500 por galon
        var equipo = Assert.Single(clsCalculoRecuperacion.CalcularEquipos(
            [Tanqueo("ABC123", new(2026, 6, 1, 0, 0, 0, BOGOTA), 20m, null, 190000m, estacion: null)]));

        Assert.Equal(9500m, equipo.ValorGalon);
        Assert.Equal(clsCalculoRecuperacion.EDS_DESCONOCIDA, equipo.Eds);
    }

    [Fact]
    public void Equipos_omite_el_tanqueo_que_no_tiene_ni_precio_ni_forma_de_calcularlo()
    {
        var equipos = clsCalculoRecuperacion.CalcularEquipos(
        [
            Tanqueo("AAA111", new(2026, 6, 1, 0, 0, 0, BOGOTA), 0m, null, 100m),      // galones 0: no se puede dividir
            Tanqueo("BBB222", new(2026, 6, 1, 0, 0, 0, BOGOTA), 10m, null, null),     // sin precio ni total
        ]);

        Assert.Empty(equipos);
    }

    [Fact]
    public void Equipos_ante_empate_de_precio_elige_el_tanqueo_mas_antiguo()
    {
        var antiguo = Tanqueo("ABC123", new(2026, 6, 3, 0, 0, 0, BOGOTA), 10m, 9000m, estacion: "Antigua");
        var reciente = Tanqueo("ABC123", new(2026, 6, 20, 0, 0, 0, BOGOTA), 10m, 9000m, estacion: "Reciente");

        var equipo = Assert.Single(clsCalculoRecuperacion.CalcularEquipos([reciente, antiguo]));

        Assert.Equal("Antigua", equipo.Eds);
    }

    [Fact]
    public void GalonesPorEds_suma_galones_y_cuenta_tanqueos_solo_de_los_casados_con_el_catalogo()
    {
        var eds = clsCalculoRecuperacion.CalcularGalonesPorEds(Muestra());

        // T1 + T2 caen en la EDS Centro (40 + 50 = 90 gal, 2 tanqueos); T3 no se pudo casar y no aparece.
        var centro = Assert.Single(eds);
        Assert.Equal(EDS_CENTRO, centro.Id);
        Assert.Equal(90m, centro.Galones);
        Assert.Equal(2, centro.Tanqueos);
        Assert.Equal("distracom", centro.Proveedor);
        Assert.Equal(9100m, centro.Precio);
        Assert.Null(centro.Ruta);
    }

    [Fact]
    public void Rutas_devuelve_los_segmentos_distintos_con_extremos_conocidos()
    {
        var rutas = clsCalculoRecuperacion.CalcularRutas(Muestra());

        // T1 y T2 comparten ruta y extremos (un solo segmento); T3 no tiene extremos.
        var ruta = Assert.Single(rutas);
        Assert.Equal(new clsRutaLinea("Bogota-Villavicencio", 4.6, -74.1, 4.15, -73.6), ruta);
    }

    [Fact]
    public void Calcular_arma_el_resultado_completo()
    {
        var r = clsCalculoRecuperacion.Calcular(Muestra());

        Assert.Equal(3, r.Totales.Tanqueos);
        Assert.Equal(2, r.Utilizacion.Count);
        Assert.Equal(2, r.Equipos.Count);
        Assert.Single(r.GalonesPorEds);
        Assert.Single(r.Rutas);
    }
}
