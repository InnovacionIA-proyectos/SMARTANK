using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Auth;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Application.Tests.Auth;

public class clsCrearAdministradorInicialCommandTests
{
    private const string CLAVE_VALIDA = "una-clave-larga-123";

    private readonly Mock<IUsuariosRepository> _usuarios = new();
    private readonly Mock<IPasswordHasherPort> _hasher = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();

    public clsCrearAdministradorInicialCommandTests()
    {
        _hasher.Setup(h => h.Hash(It.IsAny<string>())).Returns("$2a$12$hash-generado");
    }

    private clsCrearAdministradorInicialCommandHandler Handler(string? email, string? password, string? nombre = "Admin") =>
        new(Options.Create(new clsAdminInicialOptions { Email = email, Password = password, Nombre = nombre }),
            _usuarios.Object, _hasher.Object, _uow.Object, _auditoria.Object);

    [Fact]
    public async Task Crea_el_administrador_con_rol_Admin_y_la_clave_hasheada()
    {
        clsUsuario? agregado = null;
        _usuarios.Setup(u => u.ExisteEmailAsync("admin@empresa.co", It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _usuarios.Setup(u => u.Agregar(It.IsAny<clsUsuario>())).Callback<clsUsuario>(u => agregado = u);

        var resultado = await Handler("  Admin@Empresa.co ", CLAVE_VALIDA, " Administrador ").Handle(new clsCrearAdministradorInicialCommand());

        Assert.True(resultado.Creado);
        Assert.Equal("admin@empresa.co", resultado.Email);
        Assert.NotNull(agregado);
        Assert.Equal(Rol.Admin, agregado!.Rol);
        Assert.Equal("admin@empresa.co", agregado.Email);
        Assert.Equal("Administrador", agregado.Nombre);
        Assert.Equal("$2a$12$hash-generado", agregado.PasswordHash);
        Assert.DoesNotContain(CLAVE_VALIDA, agregado.PasswordHash);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task La_auditoria_no_registra_la_clave_ni_su_hash()
    {
        _usuarios.Setup(u => u.ExisteEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await Handler("admin@empresa.co", CLAVE_VALIDA).Handle(new clsCrearAdministradorInicialCommand());

        _auditoria.Verify(a => a.RegistrarAsync(
            null, "crear_admin_inicial", "tblUsuarios", It.IsAny<Guid?>(), null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Es_idempotente_si_el_correo_ya_existe_no_cambia_nada()
    {
        _usuarios.Setup(u => u.ExisteEmailAsync("admin@empresa.co", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var resultado = await Handler("admin@empresa.co", CLAVE_VALIDA).Handle(new clsCrearAdministradorInicialCommand());

        Assert.False(resultado.Creado);
        Assert.Equal("admin@empresa.co", resultado.Email);
        _usuarios.Verify(u => u.Agregar(It.IsAny<clsUsuario>()), Times.Never);
        _hasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        _auditoria.Verify(a => a.RegistrarAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_otro_proceso_lo_creo_a_la_vez_responde_como_ya_existente()
    {
        _usuarios.Setup(u => u.ExisteEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("REGISTRO_DUPLICADO", "duplicado"));

        var resultado = await Handler("admin@empresa.co", CLAVE_VALIDA).Handle(new clsCrearAdministradorInicialCommand());

        Assert.False(resultado.Creado);
        _auditoria.Verify(a => a.RegistrarAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("corta")]
    [InlineData("12345678901")]   // 11 caracteres: uno menos que el minimo
    [InlineData("")]
    [InlineData(null)]
    public async Task Clave_debil_o_ausente_lanza_ADMIN_INICIAL_INVALIDO_sin_tocar_la_base(string? clave)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler("admin@empresa.co", clave).Handle(new clsCrearAdministradorInicialCommand()));

        Assert.Equal("ADMIN_INICIAL_INVALIDO", ex.Code);
        _usuarios.Verify(u => u.ExisteEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _usuarios.Verify(u => u.Agregar(It.IsAny<clsUsuario>()), Times.Never);
    }

    [Fact]
    public async Task Clave_de_exactamente_12_caracteres_es_valida()
    {
        _usuarios.Setup(u => u.ExisteEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var resultado = await Handler("admin@empresa.co", "123456789012").Handle(new clsCrearAdministradorInicialCommand());

        Assert.True(resultado.Creado);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sin-arroba")]
    public async Task Correo_ausente_o_invalido_lanza_ADMIN_INICIAL_INVALIDO(string? email)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler(email, CLAVE_VALIDA).Handle(new clsCrearAdministradorInicialCommand()));

        Assert.Equal("ADMIN_INICIAL_INVALIDO", ex.Code);
    }

    [Fact]
    public async Task El_mensaje_de_error_no_incluye_la_clave_configurada()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(() =>
            Handler("admin@empresa.co", "secreta-1").Handle(new clsCrearAdministradorInicialCommand()));

        Assert.DoesNotContain("secreta-1", ex.Message);
    }

    [Fact]
    public async Task Sin_nombre_el_usuario_queda_con_nombre_nulo()
    {
        clsUsuario? agregado = null;
        _usuarios.Setup(u => u.ExisteEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _usuarios.Setup(u => u.Agregar(It.IsAny<clsUsuario>())).Callback<clsUsuario>(u => agregado = u);

        await Handler("admin@empresa.co", CLAVE_VALIDA, "   ").Handle(new clsCrearAdministradorInicialCommand());

        Assert.Null(agregado!.Nombre);
    }
}
