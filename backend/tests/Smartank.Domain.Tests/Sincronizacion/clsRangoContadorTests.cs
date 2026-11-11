using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsRangoContadorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Una_sola_lectura_es_inicio_y_final_a_la_vez()
    {
        var rango = new clsRangoContador();
        rango.Agregar(100, T0);

        Assert.Equal(100, rango.Inicial);
        Assert.Equal(100, rango.Final);
        Assert.Equal(1, rango.Lecturas);
    }

    [Fact]
    public void Inicio_y_final_se_eligen_por_fecha_aunque_las_lecturas_lleguen_desordenadas()
    {
        var rango = new clsRangoContador();
        rango.Agregar(500, T0.AddHours(5));
        rango.Agregar(100, T0);
        rango.Agregar(900, T0.AddHours(10));
        rango.Agregar(300, T0.AddHours(2));

        Assert.Equal(100, rango.Inicial);
        Assert.Equal(900, rango.Final);
    }

    [Fact]
    public void Un_reinicio_del_contador_se_ve_como_final_menor_que_inicial_y_no_como_maximo_y_minimo()
    {
        var rango = new clsRangoContador();
        rango.Agregar(1_000_000, T0);
        rango.Agregar(50, T0.AddDays(1));

        Assert.Equal(1_000_000, rango.Inicial);
        Assert.Equal(50, rango.Final);
    }

    [Fact]
    public void Ante_fechas_iguales_se_conserva_la_primera_lectura()
    {
        var rango = new clsRangoContador();
        rango.Agregar(10, T0);
        rango.Agregar(20, T0);

        Assert.Equal(10, rango.Inicial);
        Assert.Equal(10, rango.Final);
    }
}
