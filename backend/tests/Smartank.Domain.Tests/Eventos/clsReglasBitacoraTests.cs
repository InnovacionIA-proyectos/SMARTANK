using Smartank.Domain.Eventos.Reglas;

namespace Smartank.Domain.Tests.Eventos;

public class clsReglasBitacoraTests
{
    private static readonly DateTimeOffset INICIO = new(2026, 6, 17, 7, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FIN = INICIO.AddHours(12);

    [Fact]
    public void Dentro_de_la_ventana_es_valido()
    {
        Assert.True(clsReglasBitacora.EstaDentroDeVentana(INICIO, FIN, INICIO.AddHours(3)));
    }

    [Fact]
    public void Los_extremos_de_la_ventana_son_inclusivos()
    {
        Assert.True(clsReglasBitacora.EstaDentroDeVentana(INICIO, FIN, INICIO));
        Assert.True(clsReglasBitacora.EstaDentroDeVentana(INICIO, FIN, FIN));
    }

    [Fact]
    public void Un_instante_antes_del_inicio_o_despues_del_fin_esta_fuera()
    {
        Assert.False(clsReglasBitacora.EstaDentroDeVentana(INICIO, FIN, INICIO.AddTicks(-1)));
        Assert.False(clsReglasBitacora.EstaDentroDeVentana(INICIO, FIN, FIN.AddTicks(1)));
    }

    [Fact]
    public void La_ventana_se_compara_por_instante_sin_importar_el_desfase_horario()
    {
        // 07:00 UTC es 02:00 en Colombia (-05:00): el mismo instante, mismo resultado.
        var ahoraEnColombia = new DateTimeOffset(2026, 6, 17, 2, 0, 0, TimeSpan.FromHours(-5));

        Assert.True(clsReglasBitacora.EstaDentroDeVentana(INICIO, FIN, ahoraEnColombia));
    }

    [Theory]
    [InlineData("diesel", "diesel")]
    [InlineData("  Gasolina ", "Gasolina")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void DerivarProducto_toma_el_tipo_de_motor_recortado(string? tipoMotor, string? esperado)
    {
        Assert.Equal(esperado, clsReglasBitacora.DerivarProducto(tipoMotor));
    }
}
