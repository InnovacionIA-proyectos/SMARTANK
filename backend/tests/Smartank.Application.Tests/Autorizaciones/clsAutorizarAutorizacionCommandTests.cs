using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Autorizaciones;

public class clsAutorizarAutorizacionCommandTests
{
    private static readonly Guid USUARIO = Guid.NewGuid();

    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();

    public clsAutorizarAutorizacionCommandTests() => _usuario.SetupGet(u => u.UsuarioId).Returns(USUARIO);

    private clsAutorizarAutorizacionCommandHandler Handler() =>
        new(_autorizaciones.Object, _auditoria.Object, _uow.Object, _usuario.Object);

    private clsAutorizacionTanqueo Existente(EstadoAutorizacion estado)
    {
        var a = clsDatosPrueba.Autorizacion(estado);
        _autorizaciones.Setup(r => r.ObtenerAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        return a;
    }

    [Fact]
    public async Task Autoriza_una_propuesta_y_fija_quien_la_autorizo()
    {
        var a = Existente(EstadoAutorizacion.Propuesta);

        var dto = await Handler().Handle(new clsAutorizarAutorizacionCommand(a.Id));

        Assert.Equal(EstadoAutorizacion.Autorizada, a.Estado);
        Assert.Equal(USUARIO, a.AutorizadoPor);
        Assert.Equal(EstadoAutorizacion.Autorizada, dto.Estado);
        Assert.Equal(USUARIO, dto.AutorizadoPor);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Audita_el_cambio_con_el_diff_de_estado()
    {
        var a = Existente(EstadoAutorizacion.Propuesta);

        await Handler().Handle(new clsAutorizarAutorizacionCommand(a.Id));

        _auditoria.Verify(x => x.RegistrarAsync(
            USUARIO, "autorizar_tanqueo", "autorizaciones_tanqueo", a.Id,
            It.Is<object?>(d => clsDatosPrueba.Json(d) == "{\"estado\":[\"propuesta\",\"autorizada\"]}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Autorizada, "autorizada")]
    [InlineData(EstadoAutorizacion.Ejecutada, "ejecutada")]
    [InlineData(EstadoAutorizacion.EjecutadaParcial, "ejecutada_parcial")]
    [InlineData(EstadoAutorizacion.Expirada, "expirada")]
    [InlineData(EstadoAutorizacion.Rechazada, "rechazada")]
    public async Task Solo_se_puede_autorizar_una_propuesta(EstadoAutorizacion estado, string textoEstado)
    {
        var a = Existente(estado);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsAutorizarAutorizacionCommand(a.Id)));

        Assert.Equal("TRANSICION_INVALIDA", ex.Code);
        Assert.Contains(textoEstado, ex.Message);
        Assert.Equal(estado, a.Estado);
        Assert.Null(a.AutorizadoPor);
        _auditoria.Verify(x => x.RegistrarAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Autorizacion_inexistente_lanza_404()
    {
        _autorizaciones.Setup(r => r.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAutorizacionTanqueo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsAutorizarAutorizacionCommand(Guid.NewGuid())));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Sin_usuario_en_sesion_lanza_401_y_no_toca_la_autorizacion()
    {
        _usuario.SetupGet(u => u.UsuarioId).Returns((Guid?)null);
        var a = Existente(EstadoAutorizacion.Propuesta);

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() => Handler().Handle(new clsAutorizarAutorizacionCommand(a.Id)));

        Assert.Equal("NO_AUTENTICADO", ex.Code);
        Assert.Equal(EstadoAutorizacion.Propuesta, a.Estado);
    }

    [Fact]
    public async Task Un_conflicto_de_concurrencia_del_guardado_se_propaga_como_409()
    {
        // Otro usuario cambio la fila entre la lectura y el guardado (rowversion distinto).
        var a = Existente(EstadoAutorizacion.Propuesta);
        _auditoria.Setup(x => x.RegistrarAsync(
                It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("CONFLICTO_CONCURRENCIA", "otro usuario"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsAutorizarAutorizacionCommand(a.Id)));

        Assert.Equal("CONFLICTO_CONCURRENCIA", ex.Code);
    }
}
