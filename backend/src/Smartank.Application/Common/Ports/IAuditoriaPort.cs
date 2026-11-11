namespace Smartank.Application.Common.Ports;

/// <summary>
/// Registro de auditoria append-only (tblAuditLog): toda accion que cambia estado deja actor, accion, entidad y diff.
/// Implementado en Infrastructure. <paramref name="actorId"/> es null para acciones del sistema o de un conductor sin cuenta.
/// </summary>
public interface IAuditoriaPort
{
    Task RegistrarAsync(
        Guid? actorId,
        string accion,
        string entidad,
        Guid? entidadId,
        object? diff = null,
        CancellationToken ct = default);
}
