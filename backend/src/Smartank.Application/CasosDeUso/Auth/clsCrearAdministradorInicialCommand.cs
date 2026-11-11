using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Application.CasosDeUso.Auth.Validadores;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Application.CasosDeUso.Auth;

/// <summary>
/// Comando de un solo uso (dotnet Smartank.Api.dll crear-admin): crea el administrador inicial a partir de la seccion
/// Admin de la configuracion. Es idempotente: si el correo ya existe no hace nada ni cambia su clave.
/// </summary>
public sealed record clsCrearAdministradorInicialCommand : ICommand<clsCrearAdministradorResultado>;

public sealed class clsCrearAdministradorInicialCommandHandler(
    IOptions<clsAdminInicialOptions> opciones,
    IUsuariosRepository usuarios,
    IPasswordHasherPort hasher,
    IUnitOfWork uow,
    IAuditoriaPort auditoria)
    : ICommandHandler<clsCrearAdministradorInicialCommand, clsCrearAdministradorResultado>
{
    public async Task<clsCrearAdministradorResultado> Handle(
        clsCrearAdministradorInicialCommand command, CancellationToken ct = default)
    {
        var config = opciones.Value;
        var email = clsCredencialesValidador.NormalizarEmail(config.Email);
        clsCredencialesValidador.ValidarAdministradorInicial(email, config.Password);

        if (await usuarios.ExisteEmailAsync(email, ct))
            return new clsCrearAdministradorResultado(false, email);

        var admin = new clsUsuario
        {
            Email = email,
            PasswordHash = hasher.Hash(config.Password!),
            Nombre = string.IsNullOrWhiteSpace(config.Nombre) ? null : config.Nombre.Trim(),
            Rol = Rol.Admin,
        };
        usuarios.Agregar(admin);

        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (clsConflictException)
        {
            // Otro proceso (por ejemplo, dos replicas arrancando a la vez) lo creo entre la consulta y el guardado.
            return new clsCrearAdministradorResultado(false, email);
        }

        // Sin diff: la clave (ni su hash) no debe quedar en la bitacora.
        await auditoria.RegistrarAsync(null, "crear_admin_inicial", "tblUsuarios", admin.Id, null, ct);
        return new clsCrearAdministradorResultado(true, email);
    }
}
