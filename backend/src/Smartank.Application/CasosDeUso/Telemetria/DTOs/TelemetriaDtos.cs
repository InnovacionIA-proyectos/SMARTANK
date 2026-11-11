namespace Smartank.Application.CasosDeUso.Telemetria.DTOs;

/// <summary>Ultimo estado de tanque de un vehiculo (GET /api/vehiculos/{id}/telemetria).</summary>
public record clsTelemetriaVehiculoDto(
    Guid VehiculoId,
    string Placa,
    decimal? NivelTanqueActual,
    double? Odometro,
    bool TieneTelemetria,
    DateTimeOffset ActualizadoAt);

/// <summary>Vehiculo completo, con todos sus campos publicos (misma forma que el serializador del origen).</summary>
public record clsVehiculoDto(
    Guid Id,
    string Placa,
    string TipoMotor,
    decimal? NivelTanqueActual,
    double? RendimientoHistorico,
    double? RendimientoEsperado,
    double? Odometro,
    double ToleranciaMotor,
    double? UltimaLat,
    double? UltimaLng,
    string? Programa,
    long? FpsapId,
    decimal? CapacidadTanqueGal,
    decimal? GalonesReserva,
    decimal? GalonesUtilizables,
    decimal? GalonesTolerancia,
    string? ChipTanqueo,
    string? NitPropietario,
    string? AliasPlaca,
    bool Activo,
    string? GeotabDeviceId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Respuesta de la ingesta: { ok, vehiculo }.</summary>
public record clsIngestaTelemetriaResponse(bool Ok, clsVehiculoDto Vehiculo);
