using Smartank.Domain.Enums;

namespace Smartank.Domain.Viajes.Entidades;

/// <summary>
/// Viaje (FO: flete/orden de despacho) (tabla tblViajes). Los reales se sincronizan desde el sistema de fletes
/// (<see cref="FoId"/> unico); los creados a mano dejan los campos del FO en null.
/// </summary>
public class clsViaje
{
    public Guid Id { get; set; }

    public Guid VehiculoId { get; set; }

    public Guid? DespachadoPor { get; set; }

    /// <summary>Nombre de la ruta origen-destino (NombreRuta del FO o, si viene vacio, CodigoRuta).</summary>
    public string Ruta { get; set; } = string.Empty;

    public EstadoViaje Estado { get; set; } = EstadoViaje.Programado;

    public string? Manifiesto { get; set; }

    /// <summary>Autonomia estimada con el nivel de tanque al despachar, en km.</summary>
    public double? AutonomiaEstimadaKm { get; set; }

    /// <summary>Inicio real del recorrido; null mientras esta programado.</summary>
    public DateTimeOffset? InicioAt { get; set; }

    public DateTimeOffset? FechaDespacho { get; set; }

    /// <summary>Columna calculada por la base: <see cref="InicioAt"/> si existe, si no <see cref="FechaDespacho"/>. Solo lectura.</summary>
    public DateTimeOffset? FechaEfectiva { get; private set; }

    public long? FoId { get; set; }

    /// <summary>Numero de FO tal como viene del sistema de fletes (con ceros a la izquierda).</summary>
    public string? NumeroFo { get; set; }

    public string? PlacaCabezote { get; set; }

    public string? PlacaTrailer { get; set; }

    public string? ProgramaCabezote { get; set; }

    public string? CedulaConductor { get; set; }

    public string? Transportista { get; set; }

    public string? CodigoRuta { get; set; }

    public string? NombreRuta { get; set; }

    public string? CiudadOrigen { get; set; }

    public string? CiudadDestino { get; set; }

    public decimal? PesoFo { get; set; }

    public decimal? GalonesEds { get; set; }

    public decimal? GalonesCredito { get; set; }

    public decimal? GalonesContado { get; set; }

    public decimal? GalonesRuta { get; set; }

    public double? KmtsRuta { get; set; }

    public bool? GastosGenerados { get; set; }

    public bool? GastosEnviadoSap { get; set; }

    public bool? ConsumoEnviadoSap { get; set; }

    public bool? EnviadoSap { get; set; }

    public bool Borrado { get; set; }

    public DateTimeOffset? FoSincronizadoAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
