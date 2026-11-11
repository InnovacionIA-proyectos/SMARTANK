using Smartank.Domain.Autorizaciones.Reglas;

namespace Smartank.Domain.Tests.Autorizaciones;

/// <summary>Golden de la regla de autonomia; los tres primeros son los de autonomia.test.ts del origen.</summary>
public class clsAutonomiaTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalcularKm_aplica_la_tolerancia_del_2_por_ciento()
    {
        // 100 gal x 5 km/gal = 500 km bruto; -2 % = 490 km.
        Assert.Equal(490, clsAutonomia.CalcularKm(100, 5, 0.02), 9);
    }

    [Fact]
    public void CalcularKm_sin_tolerancia_devuelve_el_bruto()
    {
        Assert.Equal(200, clsAutonomia.CalcularKm(50, 4, 0));
    }

    [Fact]
    public void CalcularKm_rechaza_valores_negativos()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => clsAutonomia.CalcularKm(-1, 5, 0.02));
        Assert.Throws<ArgumentOutOfRangeException>(() => clsAutonomia.CalcularKm(10, -5, 0.02));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    public void CalcularKm_rechaza_una_tolerancia_fuera_de_0_a_1(double tolerancia)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => clsAutonomia.CalcularKm(10, 5, tolerancia));
    }

    [Fact]
    public void CalcularKm_con_tolerancia_total_da_cero()
    {
        Assert.Equal(0, clsAutonomia.CalcularKm(100, 5, 1));
    }

    [Fact]
    public void CalcularKm_golden_con_decimales()
    {
        // 80 x 6.5 = 520; x (1 - 0.03) = 504.4
        Assert.Equal(504.4, clsAutonomia.CalcularKm(80, 6.5, 0.03), 9);
    }

    [Fact]
    public void ResolverRendimiento_prefiere_el_historico()
    {
        var r = clsAutonomia.ResolverRendimiento(6.5, 5, 8);
        Assert.Equal(6.5, r.KmPorGalon);
        Assert.Equal(BaseRendimiento.Historico, r.Base);
    }

    [Fact]
    public void ResolverRendimiento_sin_historico_usa_el_esperado()
    {
        var r = clsAutonomia.ResolverRendimiento(null, 5, 8);
        Assert.Equal(5, r.KmPorGalon);
        Assert.Equal(BaseRendimiento.Esperado, r.Base);
    }

    [Fact]
    public void ResolverRendimiento_sin_ninguno_usa_el_default()
    {
        var r = clsAutonomia.ResolverRendimiento(null, null, 8);
        Assert.Equal(8, r.KmPorGalon);
        Assert.Equal(BaseRendimiento.Default, r.Base);
    }

    [Fact]
    public void ResolverRendimiento_un_historico_en_cero_cuenta_como_dato()
    {
        // Igual que el operador ?? del origen: solo el null cae al siguiente.
        var r = clsAutonomia.ResolverRendimiento(0, 5, 8);
        Assert.Equal(0, r.KmPorGalon);
        Assert.Equal(BaseRendimiento.Historico, r.Base);
    }

    [Fact]
    public void EsLecturaObsoleta_exactamente_en_el_limite_no_es_obsoleta()
    {
        Assert.False(clsAutonomia.EsLecturaObsoleta(AHORA.AddHours(-168), AHORA, 168));
    }

    [Fact]
    public void EsLecturaObsoleta_un_minuto_despues_del_limite_si_lo_es()
    {
        Assert.True(clsAutonomia.EsLecturaObsoleta(AHORA.AddHours(-168).AddMinutes(-1), AHORA, 168));
    }

    [Fact]
    public void EsLecturaObsoleta_una_lectura_reciente_no_lo_es()
    {
        Assert.False(clsAutonomia.EsLecturaObsoleta(AHORA.AddMinutes(-5), AHORA, 168));
    }

    [Fact]
    public void EsLecturaObsoleta_sin_fecha_de_lectura_no_se_puede_juzgar()
    {
        Assert.False(clsAutonomia.EsLecturaObsoleta(null, AHORA, 168));
    }

    [Fact]
    public void Redondear_hace_mitad_hacia_arriba()
    {
        Assert.Equal(2.35, clsAutonomia.Redondear(2.345, 2), 9);
        Assert.Equal(3, clsAutonomia.Redondear(2.5, 0));
        Assert.Equal(166.8, clsAutonomia.Redondear(166.84, 1), 9);
    }
}
