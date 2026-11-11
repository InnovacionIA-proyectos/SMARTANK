using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Application.CasosDeUso.Alertas.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Alertas.Reglas;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Alertas;

/// <summary>Control de Flota cambia el estado de gestion de una alerta (en_revision, gestionada o descartada). Los roles se exigen en Api.</summary>
public sealed record clsGestionarAlertaCommand(Guid Id, string? Estado) : ICommand<clsAlertaDto>;

public sealed class clsGestionarAlertaCommandHandler(
    IAlertasRepository alertas,
    IUnitOfWork uow,
    IAuditoriaPort auditoria,
    ICurrentUserPort usuarioActual)
    : ICommandHandler<clsGestionarAlertaCommand, clsAlertaDto>
{
    public async Task<clsAlertaDto> Handle(clsGestionarAlertaCommand command, CancellationToken ct = default)
    {
        if (!clsSnakeCase.TryParse<EstadoAlerta>(command.Estado?.Trim(), out var destino))
            throw clsListarAlertasQueryHandler.EstadoInvalido(command.Estado);

        var actorId = usuarioActual.UsuarioId
            ?? throw new clsUnauthorizedException("NO_AUTENTICADO", "No hay una sesión activa.");

        var alerta = await alertas.ObtenerParaActualizarAsync(command.Id, ct)
            ?? throw new clsNotFoundException("ALERTA_NO_ENCONTRADA", "Alerta no encontrada.");

        var anterior = alerta.Estado;
        if (!clsTransicionesAlerta.PuedePasar(anterior, destino))
            throw new clsConflictException(
                "TRANSICION_ALERTA_INVALIDA",
                $"No se puede pasar una alerta de '{clsSnakeCase.ToSnake(anterior)}' a '{clsSnakeCase.ToSnake(destino)}'.");

        alerta.Estado = destino;
        alerta.GestionadaPor = actorId;

        // Atomicidad: la transicion viaja con el rowversion que se leyo arriba. Si otro usuario cambio la alerta mientras
        // tanto, el UPDATE no encuentra la fila y se responde 409 CONFLICTO_CONCURRENCIA sin pisar su cambio.
        // RegistrarAsync guarda junto con los cambios pendientes: el cambio de estado y su auditoria son una sola transaccion.
        await auditoria.RegistrarAsync(
            actorId, "gestionar_alerta", "tblAlertas", alerta.Id,
            new { estado = new[] { clsSnakeCase.ToSnake(anterior), clsSnakeCase.ToSnake(destino) } }, ct);
        await uow.SaveChangesAsync(ct);

        return clsAlertaMapper.ADto(alerta);
    }
}
