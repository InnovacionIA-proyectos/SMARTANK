using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsNivelCombustibleTests
{
    [Theory]
    [InlineData(62.5, 150, 93.75)]    // 0.625 x 150
    [InlineData(100, 120, 120)]
    [InlineData(0, 150, 0)]
    [InlineData(50, 200.5, 100.25)]
    [InlineData(25, 80, 20)]
    public void El_nivel_en_galones_es_porcentaje_sobre_cien_por_la_capacidad_del_tanque(double porcentaje, double capacidad, double esperado)
    {
        Assert.Equal((decimal)esperado, clsNivelCombustible.Galones(porcentaje, (decimal)capacidad));
    }

    [Fact]
    public void El_resultado_se_redondea_a_dos_decimales()
    {
        // 33.333 % de 120 = 39.9996 -> 40.00
        Assert.Equal(40.00m, clsNivelCombustible.Galones(33.333, 120m));
        // 0.7 % de 150 = 1.05
        Assert.Equal(1.05m, clsNivelCombustible.Galones(0.7, 150m));
    }

    [Fact]
    public void El_odometro_de_metros_pasa_a_kilometros()
    {
        Assert.Equal(123.456, clsNivelCombustible.OdometroKm(123456), 6);
        Assert.Equal(0, clsNivelCombustible.OdometroKm(0));
    }
}
