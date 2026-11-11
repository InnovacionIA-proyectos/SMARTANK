using Moq;
using Smartank.Application.CasosDeUso.Alertas;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Alertas;

public class clsCrearAlertaCommandTests
{
    private static readonly Guid VEHICULO_ID = Guid.Parse("44444444-dddd-dddd-dddd-444444444444");
    private static readonly Guid EVENTO_ID = Guid.Parse("55555555-eeee-eeee-eeee-555555555555");

    private readonly Mock<IAlertasRepository> _alertas = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();

    public clsCrearAlertaCommandTests()
    {
        _alertas.Setup(a => a.ExisteVehiculoAsync(VEHICULO_ID, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _alertas.Setup(a => a.ExisteEventoAsync(EVENTO_ID, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private clsCrearAlertaCommandHandler Handler() => new(_alertas.Object, _uow.Object, _auditoria.Object);

    [Fact]
    public async Task Crea_la_alerta_en_estado_nueva_y_audita_sin_actor()
    {
        clsAlerta? agregada = null;
        _alertas.Setup(a => a.Agregar(It.IsAny<clsAlerta>())).Callback<clsAlerta>(a => agregada = a);

        var dto = await Handler().Handle(new clsCrearAlertaCommand("desviacion_consumo", VEHICULO_ID, EVENTO_ID, 0.2));

        Assert.NotNull(agregada);
        Assert.Equal(TipoAlerta.DesviacionConsumo, agregada!.Tipo);
        Assert.Equal(EstadoAlerta.Nueva, agregada.Estado);
        Assert.Equal(VEHICULO_ID, agregada.VehiculoId);
        Assert.Equal(EVENTO_ID, agregada.EventoId);
        Assert.Equal(0.2, agregada.MagnitudDesviacion);
        Assert.Equal(EstadoAlerta.Nueva, dto.Estado);
        Assert.Equal(TipoAlerta.DesviacionConsumo, dto.Tipo);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _auditoria.Verify(a => a.RegistrarAsync(
            null, "crear_alerta", "tblAlertas", It.IsAny<Guid?>(), It.IsNotNull<object>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Evento_y_magnitud_son_opcionales()
    {
        var dto = await Handler().Handle(new clsCrearAlertaCommand("estacion_cara", VEHICULO_ID, null, null));

        Assert.Null(dto.EventoId);
        Assert.Null(dto.MagnitudDesviacion);
        _alertas.Verify(a => a.ExisteEventoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("desviacion_consumo_carga", TipoAlerta.DesviacionConsumoCarga)]
    [InlineData("desviacion_consumo", TipoAlerta.DesviacionConsumo)]
    [InlineData("estacion_cara", TipoAlerta.EstacionCara)]
    [InlineData("ejecucion_parcial", TipoAlerta.EjecucionParcial)]
    public async Task Acepta_los_cuatro_tipos_de_alerta(string texto, TipoAlerta esperado)
    {
        var dto = await Handler().Handle(new clsCrearAlertaCommand(texto, VEHICULO_ID, null, null));

        Assert.Equal(esperado, dto.Tipo);
    }

    [Theory]
    [InlineData("otro")]
    [InlineData("DesviacionConsumo")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Tipo_invalido_lanza_TIPO_ALERTA_INVALIDO(string? tipo)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCrearAlertaCommand(tipo, VEHICULO_ID, null, null)));

        Assert.Equal("TIPO_ALERTA_INVALIDO", ex.Code);
        _uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Sin_vehiculo_lanza_VEHICULO_REQUERIDO()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCrearAlertaCommand("estacion_cara", null, null, null)));

        Assert.Equal("VEHICULO_REQUERIDO", ex.Code);
    }

    [Fact]
    public async Task Vehiculo_inexistente_lanza_VEHICULO_NO_ENCONTRADO()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsCrearAlertaCommand("estacion_cara", Guid.NewGuid(), null, null)));

        Assert.Equal("VEHICULO_NO_ENCONTRADO", ex.Code);
        _uow.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Evento_inexistente_lanza_EVENTO_NO_ENCONTRADO()
    {
        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsCrearAlertaCommand("estacion_cara", VEHICULO_ID, Guid.NewGuid(), null)));

        Assert.Equal("EVENTO_NO_ENCONTRADO", ex.Code);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(2e9)]
    public async Task Magnitud_fuera_de_rango_lanza_MAGNITUD_INVALIDA(double magnitud)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsCrearAlertaCommand("desviacion_consumo", VEHICULO_ID, null, magnitud)));

        Assert.Equal("MAGNITUD_INVALIDA", ex.Code);
    }
}
