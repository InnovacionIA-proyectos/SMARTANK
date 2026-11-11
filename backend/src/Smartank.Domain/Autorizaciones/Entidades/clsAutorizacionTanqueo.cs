using Smartank.Domain.Enums;

namespace Smartank.Domain.Autorizaciones.Entidades;

/// <summary>
/// Autorizacion de tanqueo (tabla tblAutorizacionesTanqueo): orden para cargar hasta <see cref="GalonesMax"/> en una EDS,
/// con precio techo y ventana de vigencia. Las no ejecutadas se conservan (Expirada/Rechazada), nunca se borran.
/// </summary>
public class clsAutorizacionTanqueo
{
    public Guid Id { get; set; }

    public Guid ViajeId { get; set; }

    public Guid VehiculoId { get; set; }

    public Guid EstacionId { get; set; }

    public decimal GalonesMax { get; set; }

    public decimal PrecioTecho { get; set; }

    /// <summary>Ahorro estimado frente a la estacion de referencia.</summary>
    public decimal? DeltaAhorroEstimado { get; set; }

    public DateTimeOffset FechaInicio { get; set; }

    public DateTimeOffset FechaFin { get; set; }

    public EstadoAutorizacion Estado { get; set; } = EstadoAutorizacion.Propuesta;

    public Guid? AutorizadoPor { get; set; }

    /// <summary>Respuesta de la EDS a la pre-autorizacion, como texto JSON (null si no se pre-autorizo).</summary>
    public string? EdsConfirmacion { get; set; }

    /// <summary>Token de concurrencia optimista (rowversion). Lo genera la base; no se asigna a mano.</summary>
    public byte[] RowVersion { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
