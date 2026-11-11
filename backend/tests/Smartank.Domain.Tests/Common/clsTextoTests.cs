using Smartank.Domain.Common;

namespace Smartank.Domain.Tests.Common;

public class clsTextoTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("  ABC123 ", "ABC123")]
    public void NullifyBlank_recorta_y_convierte_vacio_en_null(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, clsTexto.NullifyBlank(entrada));
    }

    [Theory]
    [InlineData("N/A", null)]
    [InlineData("n/a", null)]
    [InlineData(" x ", "x")]
    [InlineData(null, null)]
    public void NullifyNA_trata_N_A_como_nulo(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, clsTexto.NullifyNA(entrada));
    }

    [Theory]
    [InlineData("000301453730", "301453730")]
    [InlineData("301453730", "301453730")]
    [InlineData("000", "000")]
    [InlineData("  0012 ", "12")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void StripCeros_quita_ceros_a_la_izquierda_salvo_que_todo_sean_ceros(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, clsTexto.StripCeros(entrada));
    }

    [Theory]
    [InlineData("5001", "000000005001")]
    [InlineData("05001000", "000005001000")]
    [InlineData("000005001000", "000005001000")]
    [InlineData(null, null)]
    public void NormalizarCodigoDane_rellena_a_12_digitos_sin_recortar_ceros(string? entrada, string? esperado)
    {
        Assert.Equal(esperado, clsTexto.NormalizarCodigoDane(entrada));
    }

    [Theory]
    [InlineData("3046758799", "+573046758799")]
    [InlineData("573046758799", "+573046758799")]
    [InlineData("+57 304 675 8799", "+573046758799")]
    [InlineData("304-675-8799", "+573046758799")]
    public void NormalizarCelularCo_acepta_celulares_colombianos_validos(string entrada, string esperado)
    {
        Assert.Equal(esperado, clsTexto.NormalizarCelularCo(entrada));
    }

    [Theory]
    [InlineData("1020304050")] // 10 digitos que no empiezan por 3: una cedula, no un celular
    [InlineData("12345")]
    [InlineData("603046758799")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void NormalizarCelularCo_descarta_lo_que_no_es_un_celular_colombiano(string? entrada)
    {
        Assert.Null(clsTexto.NormalizarCelularCo(entrada));
    }
}
