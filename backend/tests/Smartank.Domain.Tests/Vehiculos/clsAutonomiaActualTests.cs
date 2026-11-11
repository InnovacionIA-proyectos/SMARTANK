using Smartank.Domain.Vehiculos.Reglas;

namespace Smartank.Domain.Tests.Vehiculos;

public class clsAutonomiaActualTests
{
    private const double RENDIMIENTO_DEFAULT = 8;

    [Fact]
    public void Usa_el_rendimiento_historico_cuando_existe()
    {
        // 40 gal x 8.5 km/gal x (1 - 0.02) = 333.2 -> 333
        var km = clsAutonomiaActual.Calcular(40m, 8.5, 9.0, RENDIMIENTO_DEFAULT, 0.02);
        Assert.Equal(333, km);
    }

    [Fact]
    public void Sin_historico_usa_el_esperado()
    {
        // 40 x 9 x 0.98 = 352.8 -> 353
        var km = clsAutonomiaActual.Calcular(40m, null, 9.0, RENDIMIENTO_DEFAULT, 0.02);
        Assert.Equal(353, km);
    }

    [Fact]
    public void Sin_historico_ni_esperado_usa_el_default_de_negocio()
    {
        // 40 x 8 x 0.98 = 313.6 -> 314
        var km = clsAutonomiaActual.Calcular(40m, null, null, RENDIMIENTO_DEFAULT, 0.02);
        Assert.Equal(314, km);
    }

    [Fact]
    public void Sin_lectura_de_nivel_no_hay_autonomia()
    {
        Assert.Null(clsAutonomiaActual.Calcular(null, 8.5, 9.0, RENDIMIENTO_DEFAULT, 0.02));
    }

    [Fact]
    public void Tanque_vacio_da_cero()
    {
        Assert.Equal(0, clsAutonomiaActual.Calcular(0m, 8.5, null, RENDIMIENTO_DEFAULT, 0.02));
    }

    [Fact]
    public void La_mitad_se_redondea_hacia_arriba_como_el_round_original()
    {
        // 2.5 x 5 x 1 = 12.5 exacto -> 13 (el redondeo bancario, que redondea a par, daria 12)
        Assert.Equal(13, clsAutonomiaActual.Calcular(2.5m, 5.0, null, RENDIMIENTO_DEFAULT, 0));
        // 3.5 x 5 x 1 = 17.5 exacto -> 18 (el bancario tambien daria 18: el caso anterior es el que distingue)
        Assert.Equal(18, clsAutonomiaActual.Calcular(3.5m, 5.0, null, RENDIMIENTO_DEFAULT, 0));
    }

    [Fact]
    public void Tolerancia_total_anula_la_autonomia()
    {
        Assert.Equal(0, clsAutonomiaActual.Calcular(40m, 8.5, null, RENDIMIENTO_DEFAULT, 1.0));
    }
}
