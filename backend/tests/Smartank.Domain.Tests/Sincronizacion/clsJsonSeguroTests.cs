using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsJsonSeguroTests
{
    [Fact]
    public void Un_JSON_valido_se_compacta()
    {
        var resultado = clsJsonSeguro.NormalizarONull("{ \"a\": 1,\n  \"b\": [1, 2] }");

        Assert.Equal("{\"a\":1,\"b\":[1,2]}", resultado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("esto no es json")]
    [InlineData("{\"a\":")]
    [InlineData("null")]
    public void Lo_que_no_es_un_JSON_con_dato_se_guarda_como_NULL(string? texto)
    {
        Assert.Null(clsJsonSeguro.NormalizarONull(texto));
    }
}
