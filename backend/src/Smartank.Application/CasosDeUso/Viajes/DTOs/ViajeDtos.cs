using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Viajes.DTOs;

/// <summary>Viaje activo con los datos del vehiculo y de su ruta que necesitan los paneles de despacho y seguimiento (forma <c>ViajeApi</c> del origen).</summary>
public record clsViajeDto(
    Guid Id,
    Guid VehiculoId,
    string Placa,
    string TipoMotor,
    string Ruta,
    string? NombreRuta,
    string? CodigoRuta,
    EstadoViaje Estado,
    double? AutonomiaEstimadaKm,
    decimal? NivelTanqueActual,
    decimal? CapacidadTanqueGal,
    DateTimeOffset? UbicacionAt,
    DateTimeOffset? NivelAt,
    double? AutonomiaKm,
    double? RutaKm,
    double? VehiculoLat,
    double? VehiculoLng,
    double? OrigenLat,
    double? OrigenLng,
    double? DestinoLat,
    double? DestinoLng,
    string? Manifiesto,
    DateTimeOffset? InicioAt,
    DateTimeOffset? FechaDespacho,
    bool RecomendacionActiva);

/// <summary>Valores disponibles para los filtros del panel: rutas y anios presentes entre los viajes activos.</summary>
public record clsViajesOpcionesDto(IReadOnlyList<string> Rutas, IReadOnlyList<int> Anios);

/// <summary>Respuesta de GET /api/viajes: { total, viajes, opciones }.</summary>
public record clsViajesResponse(int Total, IReadOnlyList<clsViajeDto> Viajes, clsViajesOpcionesDto Opciones);

/// <summary>Identificacion del viaje en la respuesta de ruta.</summary>
public record clsViajeResumenDto(Guid Id, string Ruta, EstadoViaje Estado, string Placa);

/// <summary>Polilinea de la ruta como [[lat, lng], ...] con su longitud total.</summary>
public record clsRutaPolilineaDto(double[][] Polilinea, double? Kilometros, int? NumPuntos);

/// <summary>Ultima posicion conocida del vehiculo.</summary>
public record clsVehiculoPosicionDto(string Placa, double Lat, double Lng, DateTimeOffset? UbicacionAt);

/// <summary>Respuesta de GET /api/viajes/{id}/ruta (forma <c>RutaViajeApi</c>). Ruta y Vehiculo son null si no se conocen.</summary>
public record clsRutaViajeDto(
    clsViajeResumenDto Viaje,
    clsRutaPolilineaDto? Ruta,
    clsVehiculoPosicionDto? Vehiculo,
    double? AutonomiaKm);
