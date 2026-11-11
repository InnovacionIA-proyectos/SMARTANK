using Moq;
using Smartank.Application.CasosDeUso.Auth;
using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Application.Tests.Auth;

public class clsLoginCommandTests
{
    private const string HASH_REAL = "$2a$12$hash-del-usuario";
    private const string HASH_FICTICIO = "$2a$12$hash-ficticio";

    private readonly Mock<IUsuariosRepository> _usuarios = new();
    private readonly Mock<IPasswordHasherPort> _hasher = new();
    private readonly Mock<ISesionTokenPort> _tokens = new();

    public clsLoginCommandTests()
    {
        _hasher.SetupGet(h => h.HashFicticio).Returns(HASH_FICTICIO);
        _tokens.Setup(t => t.Emitir(It.IsAny<clsUsuarioDto>()))
            .Returns(new clsSesionEmitida("token-jwt", new DateTimeOffset(2026, 7, 1, 12, 0, 0, TimeSpan.Zero)));
    }

    private clsLoginCommandHandler Handler() => new(_usuarios.Object, _hasher.Object, _tokens.Object);

    private static clsUsuario Maria() => new()
    {
        Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        Email = "maria.gomez@empresa.co",
        PasswordHash = HASH_REAL,
        Nombre = "María Gómez",
        Rol = Rol.ControlFlota,
    };

    [Fact]
    public async Task Credenciales_validas_devuelven_el_usuario_sin_hash_y_la_sesion()
    {
        _usuarios.Setup(u => u.ObtenerPorEmailAsync("maria.gomez@empresa.co", It.IsAny<CancellationToken>())).ReturnsAsync(Maria());
        _hasher.Setup(h => h.Verificar("clave-correcta", HASH_REAL)).Returns(true);

        var resultado = await Handler().Handle(new clsLoginCommand("maria.gomez@empresa.co", "clave-correcta"));

        Assert.Equal("maria.gomez@empresa.co", resultado.Usuario.Email);
        Assert.Equal("María Gómez", resultado.Usuario.Nombre);
        Assert.Equal(Rol.ControlFlota, resultado.Usuario.Rol);
        Assert.Equal("token-jwt", resultado.Sesion.Token);
        _tokens.Verify(t => t.Emitir(It.Is<clsUsuarioDto>(d => d.Id == Maria().Id && d.Rol == Rol.ControlFlota)), Times.Once);
    }

    [Fact]
    public async Task El_correo_se_normaliza_antes_de_buscar()
    {
        _usuarios.Setup(u => u.ObtenerPorEmailAsync("maria.gomez@empresa.co", It.IsAny<CancellationToken>())).ReturnsAsync(Maria());
        _hasher.Setup(h => h.Verificar(It.IsAny<string>(), HASH_REAL)).Returns(true);

        await Handler().Handle(new clsLoginCommand("  Maria.Gomez@Empresa.CO  ", "clave-correcta"));

        _usuarios.Verify(u => u.ObtenerPorEmailAsync("maria.gomez@empresa.co", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Clave_incorrecta_lanza_CREDENCIALES_INVALIDAS()
    {
        _usuarios.Setup(u => u.ObtenerPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(Maria());
        _hasher.Setup(h => h.Verificar("incorrecta", HASH_REAL)).Returns(false);

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() =>
            Handler().Handle(new clsLoginCommand("maria.gomez@empresa.co", "incorrecta")));

        Assert.Equal("CREDENCIALES_INVALIDAS", ex.Code);
        _tokens.Verify(t => t.Emitir(It.IsAny<clsUsuarioDto>()), Times.Never);
    }

    [Fact]
    public async Task Usuario_inexistente_lanza_el_mismo_error_y_verifica_contra_el_hash_ficticio()
    {
        _usuarios.Setup(u => u.ObtenerPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsUsuario?)null);
        // Aunque el hasher dijera "true", sin usuario no se debe entrar (el hash ficticio no pertenece a nadie).
        _hasher.Setup(h => h.Verificar(It.IsAny<string>(), HASH_FICTICIO)).Returns(true);

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() =>
            Handler().Handle(new clsLoginCommand("nadie@empresa.co", "cualquiera")));

        Assert.Equal("CREDENCIALES_INVALIDAS", ex.Code);
        // Se gasta el mismo trabajo que con un usuario real: una verificacion contra el hash ficticio.
        _hasher.Verify(h => h.Verificar("cualquiera", HASH_FICTICIO), Times.Once);
        _tokens.Verify(t => t.Emitir(It.IsAny<clsUsuarioDto>()), Times.Never);
    }

    [Fact]
    public async Task Usuario_inexistente_y_clave_errada_son_indistinguibles()
    {
        _usuarios.Setup(u => u.ObtenerPorEmailAsync("existe@empresa.co", It.IsAny<CancellationToken>())).ReturnsAsync(Maria());
        _usuarios.Setup(u => u.ObtenerPorEmailAsync("noexiste@empresa.co", It.IsAny<CancellationToken>())).ReturnsAsync((clsUsuario?)null);
        _hasher.Setup(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var errada = await Assert.ThrowsAsync<clsUnauthorizedException>(() =>
            Handler().Handle(new clsLoginCommand("existe@empresa.co", "mala")));
        var inexistente = await Assert.ThrowsAsync<clsUnauthorizedException>(() =>
            Handler().Handle(new clsLoginCommand("noexiste@empresa.co", "mala")));

        Assert.Equal(errada.Code, inexistente.Code);
        Assert.Equal(errada.Message, inexistente.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-es-un-correo")]
    [InlineData("a@b")]
    public async Task Correo_ausente_o_mal_formado_lanza_LOGIN_DATOS_INVALIDOS(string? email)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsLoginCommand(email, "clave")));

        Assert.Equal("LOGIN_DATOS_INVALIDOS", ex.Code);
        _usuarios.Verify(u => u.ObtenerPorEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Contrasena_ausente_lanza_LOGIN_DATOS_INVALIDOS(string? password)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsLoginCommand("maria.gomez@empresa.co", password)));

        Assert.Equal("LOGIN_DATOS_INVALIDOS", ex.Code);
    }

    [Fact]
    public async Task Contrasena_enorme_se_rechaza_sin_llegar_al_hasher()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler().Handle(new clsLoginCommand("maria.gomez@empresa.co", new string('x', 5000))));

        Assert.Equal("LOGIN_DATOS_INVALIDOS", ex.Code);
        _hasher.Verify(h => h.Verificar(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }
}
