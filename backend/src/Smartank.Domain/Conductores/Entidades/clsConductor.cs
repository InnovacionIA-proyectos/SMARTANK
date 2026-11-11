namespace Smartank.Domain.Conductores.Entidades;

/// <summary>Conductor (tabla tblConductores), sincronizado por cedula desde el maestro legado.</summary>
public class clsConductor
{
    public Guid Id { get; set; }

    /// <summary>Cedula unica; enlaza con tblViajes.CedulaConductor.</summary>
    public string Cedula { get; set; } = string.Empty;

    public string Nombres { get; set; } = string.Empty;

    /// <summary>Celular al que se envia la orden de tanqueo por WhatsApp (puede estar vacio).</summary>
    public string? NumeroCelular { get; set; }

    public bool Activo { get; set; } = true;

    public bool UtilizarTarjetaPrepago { get; set; }

    public long? FpsapId { get; set; }

    public DateTimeOffset? FpsapSincronizadoAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
