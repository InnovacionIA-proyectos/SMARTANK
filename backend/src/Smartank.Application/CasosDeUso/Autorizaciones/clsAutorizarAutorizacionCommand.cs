using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Autorizaciones.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones;

/// <summary>Despacho confirma una propuesta: pasa a Autorizada y queda registrado quien la autorizo.</summary>
public sealed record clsAutorizarAutorizacionCommand(Guid AutorizacionId) : ICommand<clsAutorizacionDto>;

public sealed class clsAutorizarAutorizacionCommandHandler(
    IAutorizacionesRepository autorizaciones,
    IAuditoriaPort auditoria,
    IUnitOfWork uow,
    ICurrentUserPort usuarioActual)
    : ICommandHandler<clsAutorizarAutorizacionCommand, clsAutorizacionDto>
{
    public async Task<clsAutorizacionDto> Handle(clsAutorizarAutorizacionCommand command, CancellationToken ct = default)
    {
        var usuarioId = usuarioActual.UsuarioId
            ?? throw new clsUnauthorizedException("NO_AUTENTICADO", "Se requiere una sesión para autorizar.");
        var autorizacion = await autorizaciones.ObtenerAsync(command.AutorizacionId, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "Autorización no encontrada.");

        var de = autorizacion.Estado;
        if (!clsMaquinaEstadosAutorizacion.PuedeTransicionar(de, EstadoAutorizacion.Autorizada))
            throw new clsConflictException(
                "TRANSICION_INVALIDA",
                $"Solo se puede autorizar una propuesta (estado actual: {clsSnakeCase.ToSnake(de)}).");

        autorizacion.Estado = EstadoAutorizacion.Autorizada;
        autorizacion.AutorizadoPor = usuarioId;

        // El rowversion de la autorizacion hace la transicion atomica: si otro usuario la cambio entre la lectura y este
        // guardado, el guardado falla con 409 CONFLICTO_CONCURRENCIA. La auditoria guarda en la misma transaccion.
        await auditoria.RegistrarAsync(
            usuarioId, "autorizar_tanqueo", "autorizaciones_tanqueo", autorizacion.Id,
            new { estado = new[] { clsSnakeCase.ToSnake(de), clsSnakeCase.ToSnake(EstadoAutorizacion.Autorizada) } }, ct);
        await uow.SaveChangesAsync(ct);

        return clsAutorizacionMapper.ToDto(autorizacion);
    }
}
