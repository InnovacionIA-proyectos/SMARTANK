using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones.DTOs;

/// <summary>
/// Detalle de una autorizacion con sus joins (AutorizacionDetalleApi del origen): estacion, vehiculo, viaje, conductor y ruta.
/// Los campos null significan "dato no disponible" (por ejemplo, vehiculo sin telemetria o viaje sin ruta calculada).
/// </summary>
public sealed class clsAutorizacionDetalleDto
{
    public Guid Id { get; init; }
    public Guid ViajeId { get; init; }
    public string Placa { get; init; } = string.Empty;
    public string? Manifiesto { get; init; }
    public string? ViajeRuta { get; init; }
    public string? ViajeNombreRuta { get; init; }
    public string? ViajeCodigoRuta { get; init; }

    /// <summary>Programa del cabezote en el FO.</summary>
    public string? Programa { get; init; }

    public string? CedulaConductor { get; init; }
    public string? ConductorNombre { get; init; }
    public Guid EstacionId { get; init; }
    public string EstacionNombre { get; init; } = string.Empty;
    public string? EstacionRuta { get; init; }
    public double? EstacionLat { get; init; }
    public double? EstacionLng { get; init; }
    public double? VehiculoLat { get; init; }
    public double? VehiculoLng { get; init; }
    public DateTimeOffset? VehiculoUbicacionAt { get; init; }

    /// <summary>Fecha/hora de la ultima lectura de nivel de tanque.</summary>
    public DateTimeOffset? VehiculoNivelAt { get; init; }

    public decimal? NivelTanqueActual { get; init; }
    public decimal? CapacidadTanqueGal { get; init; }

    /// <summary>Autonomia actual del vehiculo en km (entera): nivel x rendimiento x (1 - tolerancia).</summary>
    public double? AutonomiaKm { get; init; }

    public double? RutaKm { get; init; }
    public double? OrigenLat { get; init; }
    public double? OrigenLng { get; init; }
    public double? DestinoLat { get; init; }
    public double? DestinoLng { get; init; }
    public decimal GalonesMax { get; init; }
    public decimal PrecioTecho { get; init; }
    public decimal? DeltaAhorroEstimado { get; init; }
    public EstadoAutorizacion Estado { get; init; }
    public DateTimeOffset FechaInicio { get; init; }
    public DateTimeOffset FechaFin { get; init; }
}
