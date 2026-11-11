using Moq;
using Smartank.Application.CasosDeUso.Alertas;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Alertas;

public class clsGestionarAlertaCommandTests
{
    private static readonly Guid ALERTA_ID = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
    private static readonly Guid ACTOR_ID = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

    private readonly Mock<IAlertasRepository> _alertas = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<ICurrentUserPort> _actual = new();

    public clsGestionarAlertaCommandTests()
    {
        _actual.SetupGet(a => a.UsuarioId).Returns(ACTOR_ID);
    }

    private clsGestionarAlertaCommandHandler Handler() => new(_alertas.Object, _uow.Object, _auditoria.Object, _actual.Object);

    private clsAlerta AlertaEn(EstadoAlerta estado)
    {
        var alerta = new clsAlerta
        {
            Id = ALERTA_ID,
            Tipo = TipoAlerta.DesviacionConsumo,
            VehiculoId = Guid.NewGuid(),
            MagnitudDesviacion = 0.2,
            Estado = estado,
        };
        _alertas.Setup(a => a.ObtenerParaActualizarAsync(ALERTA_ID, It.IsAny<CancellationToken>())).ReturnsAsync(alerta);
        return alerta;
    }

    [Theory]
    [InlineData(EstadoAlerta.Nueva, "en_revision", EstadoAlerta.EnRevision)]
    [InlineData(EstadoAlerta.Nueva, "gestionada", EstadoAlerta.Gestionada)]
    [InlineData(EstadoAlerta.Nueva, "descartada", EstadoAlerta.Descartada)]
    [InlineData(EstadoAlerta.EnRevision, "gestionada", EstadoAlerta.Gestionada)]
    [InlineData(EstadoAlerta.EnRevision, "descartada", EstadoAlerta.Descartada)]
    public async Task Transicion_valida_cambia_el_estado_y_registra_quien_la_gestiono(
        EstadoAlerta actual, string destinoTexto, EstadoAlerta destino)
    {
        var alerta = AlertaEn(actual);

        var dto = await Handler().Handle(new clsGestionarAlertaCommand(ALERTA_ID, destinoTexto));

        Assert.Equal(destino, alerta.Estado);
        Assert.Equal(ACTOR_ID, alerta.GestionadaPor);
        Assert.Equal(destino, dto.Estado);
        Assert.Equal(ACTOR_ID, dto.GestionadaPor);
        Assert.Equal(ALERTA_ID, dto.Id);
    }

    [Fact]
    public async Task Deja_auditoria_con_el_estado_anterior_y_el_nuevo_y_guarda_una_vez()
    {
        AlertaEn(EstadoAlerta.Nueva);

        await Handler().Handle(new clsGestionarAlertaCommand(ALERTA_ID, "en_revision"));

        _auditoria.Verify(a => a.RegistrarAsync(
            ACTOR_ID, "gestionar_alerta", "tblAlertas", ALERTA_ID, It.IsNotNull<object>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(EstadoAlerta.Nueva, "nueva")]
    [InlineData(EstadoAlerta.EnRevision, "nueva")]
    [InlineData(EstadoAlerta.EnRevision, "en_revision")]
    [InlineData(EstadoAlerta.Gestionada, "en_revision")]
    [InlineData(EstadoAlerta.Gestionada, "descartada")]
    [InlineData(EstadoAlerta.Descartada, "gestionada")]
    public async Task Transicion_invalida_lanza_TRANSICION_ALERTA_INVALIDA_y_no_toca_nada(EstadoAlerta actual, string destinoTexto)
    {
        var alerta = AlertaEn(actual);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsGestionarAlertaCommand(ALERTA_ID, destinoTexto)));

        Assert.Equal("TRANSICION_ALERTA_INVALIDA", ex.Code);
        Assert.Equal(actual, alerta.Estado);
        Assert.Null(alerta.GestionadaPor);
        _auditoria.VerifyNoOtherCalls();
        _uow.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("volando")]
    [InlineData("EnRevision")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Estado_que_no_existe_lanza_ESTADO_ALERTA_INVALIDO(string? estado)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsGestionarAlertaCommand(ALERTA_ID, estado)));

        Assert.Equal("ESTADO_ALERTA_INVALIDO", ex.Code);
        _alertas.Verify(a => a.ObtenerParaActualizarAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Alerta_inexistente_lanza_ALERTA_NO_ENCONTRADA()
    {
        _alertas.Setup(a => a.ObtenerParaActualizarAsync(ALERTA_ID, It.IsAny<CancellationToken>())).ReturnsAsync((clsAlerta?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() =>
            Handler().Handle(new clsGestionarAlertaCommand(ALERTA_ID, "gestionada")));

        Assert.Equal("ALERTA_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Sin_usuario_en_sesion_lanza_NO_AUTENTICADO()
    {
        AlertaEn(EstadoAlerta.Nueva);
        _actual.SetupGet(a => a.UsuarioId).Returns((Guid?)null);

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() =>
            Handler().Handle(new clsGestionarAlertaCommand(ALERTA_ID, "gestionada")));

        Assert.Equal("NO_AUTENTICADO", ex.Code);
    }

    [Fact]
    public async Task Si_otro_usuario_la_modifico_a_la_vez_el_conflicto_de_concurrencia_se_propaga()
    {
        AlertaEn(EstadoAlerta.Nueva);
        // El repositorio guarda con el rowversion leido: si cambio, el guardado lanza este conflicto y no se pisa el otro cambio.
        _auditoria.Setup(a => a.RegistrarAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("CONFLICTO_CONCURRENCIA", "modificado"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() =>
            Handler().Handle(new clsGestionarAlertaCommand(ALERTA_ID, "gestionada")));

        Assert.Equal("CONFLICTO_CONCURRENCIA", ex.Code);
    }
}
