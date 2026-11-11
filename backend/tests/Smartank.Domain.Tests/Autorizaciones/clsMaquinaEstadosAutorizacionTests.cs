using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Enums;

namespace Smartank.Domain.Tests.Autorizaciones;

public class clsMaquinaEstadosAutorizacionTests
{
    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta, EstadoAutorizacion.Autorizada)]
    [InlineData(EstadoAutorizacion.Propuesta, EstadoAutorizacion.Rechazada)]
    [InlineData(EstadoAutorizacion.Propuesta, EstadoAutorizacion.Expirada)]
    [InlineData(EstadoAutorizacion.Autorizada, EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.Autorizada, EstadoAutorizacion.EjecutadaParcial)]
    [InlineData(EstadoAutorizacion.Autorizada, EstadoAutorizacion.Expirada)]
    public void Transiciones_validas(EstadoAutorizacion de, EstadoAutorizacion a)
    {
        Assert.True(clsMaquinaEstadosAutorizacion.PuedeTransicionar(de, a));
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta, EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.Propuesta, EstadoAutorizacion.EjecutadaParcial)]
    [InlineData(EstadoAutorizacion.Propuesta, EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Autorizada, EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Autorizada, EstadoAutorizacion.Autorizada)]
    [InlineData(EstadoAutorizacion.Autorizada, EstadoAutorizacion.Rechazada)]
    [InlineData(EstadoAutorizacion.Ejecutada, EstadoAutorizacion.Autorizada)]
    [InlineData(EstadoAutorizacion.Ejecutada, EstadoAutorizacion.EjecutadaParcial)]
    [InlineData(EstadoAutorizacion.EjecutadaParcial, EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.Expirada, EstadoAutorizacion.Autorizada)]
    [InlineData(EstadoAutorizacion.Rechazada, EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Rechazada, EstadoAutorizacion.Autorizada)]
    public void Transiciones_invalidas(EstadoAutorizacion de, EstadoAutorizacion a)
    {
        Assert.False(clsMaquinaEstadosAutorizacion.PuedeTransicionar(de, a));
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.EjecutadaParcial)]
    [InlineData(EstadoAutorizacion.Expirada)]
    [InlineData(EstadoAutorizacion.Rechazada)]
    public void Los_estados_finales_no_tienen_salida(EstadoAutorizacion estado)
    {
        Assert.True(clsMaquinaEstadosAutorizacion.EsFinal(estado));
        Assert.Empty(clsMaquinaEstadosAutorizacion.TransicionesDesde(estado));
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Autorizada)]
    public void Propuesta_y_autorizada_no_son_finales(EstadoAutorizacion estado)
    {
        Assert.False(clsMaquinaEstadosAutorizacion.EsFinal(estado));
    }

    [Fact]
    public void Todos_los_estados_estan_definidos_en_la_maquina()
    {
        // Si alguien agrega un estado al enum y olvida la maquina, este test falla (KeyNotFound) en vez de aceptar en silencio.
        foreach (var estado in Enum.GetValues<EstadoAutorizacion>())
            Assert.NotNull(clsMaquinaEstadosAutorizacion.TransicionesDesde(estado));
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Ejecutada, true)]
    [InlineData(EstadoAutorizacion.EjecutadaParcial, true)]
    [InlineData(EstadoAutorizacion.Autorizada, false)]
    [InlineData(EstadoAutorizacion.Propuesta, false)]
    [InlineData(EstadoAutorizacion.Expirada, false)]
    public void FueEjecutada_solo_para_ejecutada_y_parcial(EstadoAutorizacion estado, bool esperado)
    {
        Assert.Equal(esperado, clsMaquinaEstadosAutorizacion.FueEjecutada(estado));
    }
}
