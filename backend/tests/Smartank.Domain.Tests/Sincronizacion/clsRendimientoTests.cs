using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsRendimientoTests
{
    // 378.5411784 L son exactamente 100 galones (3.785411784 L/gal).
    private const double LITROS_100_GALONES = 378.5411784;

    [Fact]
    public void Ochocientos_km_con_cien_galones_rinden_ocho_km_por_galon()
    {
        var r = clsRendimiento.Calcular(1000, 1000 + LITROS_100_GALONES, 5_000_000, 5_800_000);

        Assert.Equal(ResultadoRendimiento.Calculado, r.Resultado);
        Assert.Equal(8.0, r.KmPorGalon);
    }

    [Fact]
    public void El_resultado_se_redondea_a_dos_decimales()
    {
        // 833 km / 100 gal = 8.33
        var r = clsRendimiento.Calcular(0, LITROS_100_GALONES, 0, 833_000);

        Assert.Equal(8.33, r.KmPorGalon);
    }

    [Fact]
    public void Menos_de_30_galones_en_la_ventana_no_alcanzan_para_un_promedio_estable()
    {
        // 113.5 L = 29.98 gal
        var r = clsRendimiento.Calcular(0, 113.5, 0, 500_000);

        Assert.Equal(ResultadoRendimiento.SinDatos, r.Resultado);
        Assert.Null(r.KmPorGalon);
    }

    [Fact]
    public void Menos_de_100_km_recorridos_tampoco_alcanzan()
    {
        var r = clsRendimiento.Calcular(0, LITROS_100_GALONES, 0, 99_000);

        Assert.Equal(ResultadoRendimiento.SinDatos, r.Resultado);
    }

    [Fact]
    public void Un_contador_que_se_reinicio_da_delta_negativo_y_cuenta_como_sin_datos()
    {
        var r = clsRendimiento.Calcular(900_000, 1_000, 0, 700_000);

        Assert.Equal(ResultadoRendimiento.SinDatos, r.Resultado);
    }

    [Theory]
    [InlineData(200.0, 150.0)]    // 150 km con 200 gal = 0.75 km/gal: por debajo de 1
    [InlineData(31.0, 1000.0)]    // 1000 km con 31 gal = 32.26 km/gal: por encima de 30
    public void Fuera_de_la_banda_de_1_a_30_km_por_galon_el_dato_se_descarta(double galones, double km)
    {
        var r = clsRendimiento.Calcular(0, galones * clsRendimiento.LITROS_POR_GALON, 0, km * 1000);

        Assert.Equal(ResultadoRendimiento.FueraDeBanda, r.Resultado);
        Assert.Null(r.KmPorGalon);
    }

    [Fact]
    public void Los_limites_de_la_banda_son_inclusivos()
    {
        // 100 km con 100 gal = 1.00 km/gal ; 3000 km con 100 gal = 30.00 km/gal
        Assert.Equal(1.0, clsRendimiento.Calcular(0, LITROS_100_GALONES, 0, 100_000).KmPorGalon);
        Assert.Equal(30.0, clsRendimiento.Calcular(0, LITROS_100_GALONES, 0, 3_000_000).KmPorGalon);
    }
}
