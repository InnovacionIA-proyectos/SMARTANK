using Smartank.Domain.Alertas.Reglas;
using Smartank.Domain.Enums;

namespace Smartank.Domain.Tests.Alertas;

public class clsTransicionesAlertaTests
{
    [Theory]
    [InlineData(EstadoAlerta.Nueva, EstadoAlerta.EnRevision)]
    [InlineData(EstadoAlerta.Nueva, EstadoAlerta.Gestionada)]
    [InlineData(EstadoAlerta.Nueva, EstadoAlerta.Descartada)]
    [InlineData(EstadoAlerta.EnRevision, EstadoAlerta.Gestionada)]
    [InlineData(EstadoAlerta.EnRevision, EstadoAlerta.Descartada)]
    public void Transiciones_permitidas(EstadoAlerta actual, EstadoAlerta destino)
    {
        Assert.True(clsTransicionesAlerta.PuedePasar(actual, destino));
    }

    [Theory]
    [InlineData(EstadoAlerta.Nueva, EstadoAlerta.Nueva)]
    [InlineData(EstadoAlerta.EnRevision, EstadoAlerta.Nueva)]
    [InlineData(EstadoAlerta.EnRevision, EstadoAlerta.EnRevision)]
    [InlineData(EstadoAlerta.Gestionada, EstadoAlerta.Nueva)]
    [InlineData(EstadoAlerta.Gestionada, EstadoAlerta.EnRevision)]
    [InlineData(EstadoAlerta.Gestionada, EstadoAlerta.Descartada)]
    [InlineData(EstadoAlerta.Descartada, EstadoAlerta.Gestionada)]
    [InlineData(EstadoAlerta.Descartada, EstadoAlerta.EnRevision)]
    public void Transiciones_no_permitidas(EstadoAlerta actual, EstadoAlerta destino)
    {
        Assert.False(clsTransicionesAlerta.PuedePasar(actual, destino));
    }

    [Fact]
    public void Gestionada_y_descartada_son_estados_finales()
    {
        Assert.True(clsTransicionesAlerta.EsFinal(EstadoAlerta.Gestionada));
        Assert.True(clsTransicionesAlerta.EsFinal(EstadoAlerta.Descartada));
        Assert.False(clsTransicionesAlerta.EsFinal(EstadoAlerta.Nueva));
        Assert.False(clsTransicionesAlerta.EsFinal(EstadoAlerta.EnRevision));
    }

    [Fact]
    public void Ningun_estado_permite_volver_a_nueva()
    {
        foreach (var estado in Enum.GetValues<EstadoAlerta>())
            Assert.DoesNotContain(EstadoAlerta.Nueva, clsTransicionesAlerta.Destinos(estado));
    }
}
