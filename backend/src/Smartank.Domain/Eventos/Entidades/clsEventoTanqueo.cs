namespace Smartank.Domain.Eventos.Entidades;

/// <summary>
/// Evento de tanqueo, bitacora del conductor (tabla tblEventosTanqueo). Maximo uno por autorizacion
/// (indice unico en la base).
/// </summary>
public class clsEventoTanqueo
{
    public Guid Id { get; set; }

    public Guid AutorizacionId { get; set; }

    public Guid VehiculoId { get; set; }

    public Guid EstacionId { get; set; }

    public decimal GalonesCargados { get; set; }

    public decimal? PrecioPagado { get; set; }

    /// <summary>URL corta de la foto del surtidor (/api/uploads/{id}).</summary>
    public string? FotoSurtidorUrl { get; set; }

    public double? OdometroEvento { get; set; }

    public decimal? NivelAntes { get; set; }

    public decimal? NivelDespues { get; set; }

    /// <summary>Null cuando lo registra un conductor desde el enlace de la bitacora (sin cuenta).</summary>
    public Guid? RegistradoPor { get; set; }

    /// <summary>Instantanea del programa del vehiculo al registrar el tanqueo.</summary>
    public string? Programa { get; set; }

    /// <summary>Combustible cargado (derivado del tipo de motor).</summary>
    public string? Producto { get; set; }

    public DateTimeOffset OcurridoAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
