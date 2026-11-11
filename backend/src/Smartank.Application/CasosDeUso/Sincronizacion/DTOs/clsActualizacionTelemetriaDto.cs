namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>
/// Telemetria a escribir en un vehiculo. Un campo nulo NO borra el valor almacenado (se conserva el anterior): un vehiculo
/// sin lectura nueva de nivel mantiene la ultima conocida.
/// </summary>
public sealed record clsActualizacionTelemetriaDto(
    Guid VehiculoId,
    double? Lat,
    double? Lng,
    DateTimeOffset? UbicacionAt,
    decimal? NivelGalones,
    DateTimeOffset? NivelAt,
    double? OdometroKm,
    string GeotabDeviceId);
