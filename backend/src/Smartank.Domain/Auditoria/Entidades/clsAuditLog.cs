namespace Smartank.Domain.Auditoria.Entidades;

/// <summary>Registro de auditoria APPEND-ONLY (tabla tblAuditLog): la base bloquea UPDATE y DELETE con un trigger.</summary>
public class clsAuditLog
{
    public Guid Id { get; set; }

    /// <summary>Usuario que ejecuto la accion; null para acciones del sistema o de un conductor sin cuenta.</summary>
    public Guid? ActorId { get; set; }

    /// <summary>Accion ejecutada, p. ej. "autorizar_tanqueo".</summary>
    public string Accion { get; set; } = string.Empty;

    public string Entidad { get; set; } = string.Empty;

    public Guid? EntidadId { get; set; }

    /// <summary>Estado anterior/posterior como texto JSON.</summary>
    public string? Diff { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Igual a <see cref="CreatedAt"/>: la tabla es inmutable.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
