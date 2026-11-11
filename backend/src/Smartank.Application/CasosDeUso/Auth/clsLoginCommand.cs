using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Application.CasosDeUso.Auth.Mappers;
using Smartank.Application.CasosDeUso.Auth.Validadores;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Auth;

/// <summary>Inicio de sesion con correo y contrasena contra la base propia.</summary>
public sealed record clsLoginCommand(string? Email, string? Password) : ICommand<clsLoginResultado>;

public sealed class clsLoginCommandHandler(
    IUsuariosRepository usuarios,
    IPasswordHasherPort hasher,
    ISesionTokenPort tokens)
    : ICommandHandler<clsLoginCommand, clsLoginResultado>
{
    public async Task<clsLoginResultado> Handle(clsLoginCommand command, CancellationToken ct = default)
    {
        var email = clsCredencialesValidador.NormalizarEmail(command.Email);
        clsCredencialesValidador.ValidarLogin(email, command.Password);

        var usuario = await usuarios.ObtenerPorEmailAsync(email, ct);

        // Se verifica SIEMPRE (contra un hash ficticio si el correo no existe): sin esto, un correo inexistente
        // respondería en milisegundos y uno real en cientos, y el tiempo delataria que cuentas existen.
        var claveCorrecta = hasher.Verificar(command.Password!, usuario?.PasswordHash ?? hasher.HashFicticio);

        // Mismo mensaje para correo inexistente y clave errada: no se revela cual fallo.
        if (usuario is null || !claveCorrecta)
            throw new clsUnauthorizedException("CREDENCIALES_INVALIDAS", "Credenciales inválidas.");

        var dto = clsUsuarioMapper.ADto(usuario);
        return new clsLoginResultado(dto, tokens.Emitir(dto));
    }
}
