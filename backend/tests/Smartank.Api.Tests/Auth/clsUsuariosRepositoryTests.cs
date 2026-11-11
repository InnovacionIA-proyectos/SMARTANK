using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Smartank.Api.Tests.Infra;
using Smartank.Application.CasosDeUso.Auth;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Infrastructure.Auth;
using Smartank.Infrastructure.Auth.Adaptadores;
using Smartank.Infrastructure.Auth.Repositorios;
using Smartank.Infrastructure.Common;

namespace Smartank.Api.Tests.Auth;

/// <summary>Usuarios y administrador inicial contra SQL Server (se omite sin TEST_DB_CONNECTION). Cada prueba revierte sus datos.</summary>
public class clsUsuariosRepositoryTests
{
    private static string EmailUnico(clsEscenarioBd e) => $"{e.Marca.ToLowerInvariant()}.{Guid.NewGuid():N}@prueba.co";

    private static async Task<clsUsuario> AgregarAsync(clsEscenarioBd e, string email, Rol rol = Rol.Despacho)
    {
        var u = new clsUsuario { Email = email, PasswordHash = "$2a$04$hash", Nombre = "Prueba", Rol = rol };
        e.Db.Usuarios.Add(u);
        await e.Db.SaveChangesAsync();
        return u;
    }

    [clsHechoConBaseDePruebas]
    public async Task Busca_por_correo_sin_distinguir_mayusculas_y_por_id()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var email = EmailUnico(e);
        var u = await AgregarAsync(e, email, Rol.Finanzas);
        var repo = new clsEfUsuariosRepository(e.Db);

        var porEmail = await repo.ObtenerPorEmailAsync(email.ToUpperInvariant(), default);
        var porId = await repo.ObtenerPorIdAsync(u.Id, default);

        Assert.Equal(u.Id, porEmail!.Id);
        Assert.Equal(Rol.Finanzas, porEmail.Rol);
        Assert.Equal("$2a$04$hash", porEmail.PasswordHash);
        Assert.Equal(email, porId!.Email);
        Assert.Null(await repo.ObtenerPorEmailAsync("nadie@prueba.co", default));
        Assert.Null(await repo.ObtenerPorIdAsync(Guid.NewGuid(), default));
    }

    [clsHechoConBaseDePruebas]
    public async Task ExisteEmail_consulta_la_base()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var email = EmailUnico(e);
        await AgregarAsync(e, email);
        var repo = new clsEfUsuariosRepository(e.Db);

        Assert.True(await repo.ExisteEmailAsync(email, default));
        Assert.True(await repo.ExisteEmailAsync(email.ToUpperInvariant(), default));
        Assert.False(await repo.ExisteEmailAsync("otro@prueba.co", default));
    }

    [clsHechoConBaseDePruebas]
    public async Task Crear_administrador_inicial_persiste_un_Admin_con_bcrypt_y_es_idempotente()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var email = EmailUnico(e);
        var hasher = new clsBcryptPasswordHasher(Options.Create(new clsSeguridadOptions { BcryptRounds = 4 }));
        var handler = new clsCrearAdministradorInicialCommandHandler(
            Options.Create(new clsAdminInicialOptions { Email = email.ToUpperInvariant(), Password = "una-clave-larga-123", Nombre = "Administrador" }),
            new clsEfUsuariosRepository(e.Db), hasher, new clsEfUnitOfWork(e.Db), new clsEfAuditoriaRepository(e.Db));

        var primera = await handler.Handle(new clsCrearAdministradorInicialCommand());
        var segunda = await handler.Handle(new clsCrearAdministradorInicialCommand());

        Assert.True(primera.Creado);
        Assert.False(segunda.Creado);
        var guardado = await e.Db.Usuarios.AsNoTracking().SingleAsync(u => u.Email == email);
        Assert.Equal(Rol.Admin, guardado.Rol);
        Assert.Equal("Administrador", guardado.Nombre);
        Assert.True(hasher.Verificar("una-clave-larga-123", guardado.PasswordHash));
        Assert.DoesNotContain("una-clave-larga-123", guardado.PasswordHash);
        // Una sola fila y una sola auditoria, y esta no contiene la clave.
        Assert.Equal(1, await e.Db.Usuarios.CountAsync(u => u.Email == email));
        var auditoria = await e.Db.AuditLog.SingleAsync(a => a.EntidadId == guardado.Id);
        Assert.Equal("crear_admin_inicial", auditoria.Accion);
        Assert.Null(auditoria.Diff);
    }

    [clsHechoConBaseDePruebas]
    public async Task El_rol_se_guarda_como_texto_snake_case_de_la_base()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var u = await AgregarAsync(e, EmailUnico(e), Rol.ControlFlota);

        var crudo = await e.Db.Database.SqlQuery<string>($"SELECT [Rol] AS [Value] FROM [dbo].[tblUsuarios] WHERE [Id] = {u.Id}").SingleAsync();

        Assert.Equal("control_flota", crudo);
    }
}
