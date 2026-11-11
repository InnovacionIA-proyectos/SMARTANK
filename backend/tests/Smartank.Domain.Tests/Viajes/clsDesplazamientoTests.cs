using Smartank.Domain.Viajes.Reglas;

namespace Smartank.Domain.Tests.Viajes;

public class clsDesplazamientoTests
{
    [Theory]
    [InlineData("3200000001", true)]   // 10 caracteres y empieza con 32: vacio
    [InlineData("32ABCDEFGH", true)]
    [InlineData("320000001", false)]   // 9 caracteres
    [InlineData("32000000012", false)] // 11 caracteres
    [InlineData("4200000001", false)]  // no empieza con 32
    [InlineData("1032000000", false)]  // "32" no esta al inicio
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsVacio_exige_prefijo_32_y_diez_caracteres(string? manifiesto, bool esperado)
    {
        Assert.Equal(esperado, clsDesplazamiento.EsVacio(manifiesto));
    }
}
