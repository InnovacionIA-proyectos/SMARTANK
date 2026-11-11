using Moq;
using Smartank.Application.CasosDeUso.Auth;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Application.Tests.Auth;

public class clsObtenerSesionActualQueryTests
{
    private static readonly Guid ID = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly Mock<ICurrentUserPort> _actual = new();
    private readonly Mock<IUsuariosRepository> _usuarios = new();

    private clsObtenerSesionActualQueryHandler Handler() => new(_actual.Object, _usuarios.Object);

    [Fact]
    public async Task Devuelve_el_usuario_vigente_de_la_sesion()
    {
        _actual.SetupGet(a => a.UsuarioId).Returns(ID);
        _usuarios.Setup(u => u.ObtenerPorIdAsync(ID, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsUsuario { Id = ID, Email = "luis@empresa.co", Nombre = "Luis", Rol = Rol.Finanzas, PasswordHash = "secreto" });

        var dto = await Handler().Handle(new clsObtenerSesionActualQuery());

        Assert.Equal(ID, dto.Id);
        Assert.Equal("luis@empresa.co", dto.Email);
        Assert.Equal("Luis", dto.Nombre);
        Assert.Equal(Rol.Finanzas, dto.Rol);
    }

    [Fact]
    public async Task Sin_sesion_lanza_NO_AUTENTICADO()
    {
        _actual.SetupGet(a => a.UsuarioId).Returns((Guid?)null);

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() => Handler().Handle(new clsObtenerSesionActualQuery()));

        Assert.Equal("NO_AUTENTICADO", ex.Code);
    }

    [Fact]
    public async Task Usuario_eliminado_despues_de_emitir_el_token_lanza_SESION_INVALIDA()
    {
        _actual.SetupGet(a => a.UsuarioId).Returns(ID);
        _usuarios.Setup(u => u.ObtenerPorIdAsync(ID, It.IsAny<CancellationToken>())).ReturnsAsync((clsUsuario?)null);

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() => Handler().Handle(new clsObtenerSesionActualQuery()));

        Assert.Equal("SESION_INVALIDA", ex.Code);
    }
}
