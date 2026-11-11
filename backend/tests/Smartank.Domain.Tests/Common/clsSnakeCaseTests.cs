using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Domain.Tests.Common;

public class clsSnakeCaseTests
{
    [Theory]
    [InlineData("EjecutadaParcial", "ejecutada_parcial")]
    [InlineData("ControlFlota", "control_flota")]
    [InlineData("DesviacionConsumoCarga", "desviacion_consumo_carga")]
    [InlineData("Admin", "admin")]
    [InlineData("EnRuta", "en_ruta")]
    [InlineData("", "")]
    public void ToSnake_convierte_pascal_case(string entrada, string esperado)
    {
        Assert.Equal(esperado, clsSnakeCase.ToSnake(entrada));
    }

    [Fact]
    public void Parse_es_inverso_de_ToSnake_para_todos_los_miembros_de_cada_enum()
    {
        AssertIdaYVuelta<Rol>();
        AssertIdaYVuelta<EstadoViaje>();
        AssertIdaYVuelta<EstadoAutorizacion>();
        AssertIdaYVuelta<TipoAlerta>();
        AssertIdaYVuelta<EstadoAlerta>();
        AssertIdaYVuelta<FuenteRuta>();
        AssertIdaYVuelta<FuenteTanqueo>();
    }

    [Fact]
    public void Los_valores_snake_case_coinciden_con_el_contrato_del_api()
    {
        Assert.Equal("en_ruta", clsSnakeCase.ToSnake(EstadoViaje.EnRuta));
        Assert.Equal("ejecutada_parcial", clsSnakeCase.ToSnake(EstadoAutorizacion.EjecutadaParcial));
        Assert.Equal("control_flota", clsSnakeCase.ToSnake(Rol.ControlFlota));
        Assert.Equal("en_revision", clsSnakeCase.ToSnake(EstadoAlerta.EnRevision));
        Assert.Equal("desviacion_consumo_carga", clsSnakeCase.ToSnake(TipoAlerta.DesviacionConsumoCarga));
    }

    [Fact]
    public void Parse_con_un_texto_invalido_lanza_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => clsSnakeCase.Parse<EstadoViaje>("volando"));
    }

    [Fact]
    public void TryParse_devuelve_false_en_vez_de_lanzar()
    {
        Assert.False(clsSnakeCase.TryParse<EstadoViaje>("volando", out _));
        Assert.False(clsSnakeCase.TryParse<EstadoViaje>(null, out _));
        Assert.True(clsSnakeCase.TryParse<EstadoViaje>("en_ruta", out var estado));
        Assert.Equal(EstadoViaje.EnRuta, estado);
    }

    private static void AssertIdaYVuelta<TEnum>() where TEnum : struct, Enum
    {
        foreach (var miembro in Enum.GetValues<TEnum>())
            Assert.Equal(miembro, clsSnakeCase.Parse<TEnum>(clsSnakeCase.ToSnake(miembro)));
    }
}
