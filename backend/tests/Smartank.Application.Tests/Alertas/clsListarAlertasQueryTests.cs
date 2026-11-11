using Moq;
using Smartank.Application.CasosDeUso.Alertas;
using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Alertas;

public class clsListarAlertasQueryTests
{
    private readonly Mock<IAlertasRepository> _alertas = new();

    private clsListarAlertasQueryHandler Handler() => new(_alertas.Object);

    private static clsAlertaListadoDto Fila(EstadoAlerta estado, string placa) => new(
        Guid.NewGuid(), TipoAlerta.DesviacionConsumo, Guid.NewGuid(), null, 0.2, estado, null,
        new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 6, 15, 8, 0, 0, TimeSpan.Zero), placa);

    [Fact]
    public async Task Sin_filtro_lista_todas_y_devuelve_estado_nulo()
    {
        _alertas.Setup(a => a.ListarAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Fila(EstadoAlerta.Nueva, "SNU123"), Fila(EstadoAlerta.Gestionada, "WGK456")]);

        var respuesta = await Handler().Handle(new clsListarAlertasQuery(null));

        Assert.Null(respuesta.Estado);
        Assert.Equal(2, respuesta.Total);
        Assert.Equal("SNU123", respuesta.Alertas[0].Placa);
    }

    [Fact]
    public async Task Con_estado_filtra_y_lo_repite_en_la_respuesta()
    {
        _alertas.Setup(a => a.ListarAsync(EstadoAlerta.EnRevision, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Fila(EstadoAlerta.EnRevision, "SNU123")]);

        var respuesta = await Handler().Handle(new clsListarAlertasQuery("en_revision"));

        Assert.Equal(EstadoAlerta.EnRevision, respuesta.Estado);
        Assert.Equal(1, respuesta.Total);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Estado_en_blanco_no_filtra(string estado)
    {
        _alertas.Setup(a => a.ListarAsync(null, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var respuesta = await Handler().Handle(new clsListarAlertasQuery(estado));

        Assert.Null(respuesta.Estado);
        Assert.Equal(0, respuesta.Total);
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("EnRevision")]
    [InlineData("NUEVA")]
    public async Task Estado_invalido_lanza_ESTADO_ALERTA_INVALIDO(string estado)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() => Handler().Handle(new clsListarAlertasQuery(estado)));

        Assert.Equal("ESTADO_ALERTA_INVALIDO", ex.Code);
        _alertas.VerifyNoOtherCalls();
    }
}
