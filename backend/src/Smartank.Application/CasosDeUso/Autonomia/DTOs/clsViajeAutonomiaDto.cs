using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autonomia.DTOs;

/// <summary>Viaje tal como lo expone el API de autonomia (serializeViaje del origen).</summary>
public sealed record clsViajeAutonomiaDto(
    Guid Id,
    Guid VehiculoId,
    string Ruta,
    EstadoViaje Estado,
    double? AutonomiaEstimadaKm,
    Guid? DespachadoPor,
    string? Manifiesto,
    DateTimeOffset? InicioAt,
    long? FoId,
    string? PlacaCabezote,
    string? PlacaTrailer,
    string? ProgramaCabezote,
    string? CedulaConductor,
    string? Transportista,
    string? NumeroFo,
    string? CodigoRuta,
    string? NombreRuta,
    string? CiudadOrigen,
    string? CiudadDestino,
    decimal? PesoFo,
    decimal? GalonesEds,
    decimal? GalonesCredito,
    decimal? GalonesContado,
    decimal? GalonesRuta,
    double? KmtsRuta,
    DateTimeOffset? FechaDespacho,
    bool? GastosGenerados,
    bool? GastosEnviadoSap,
    bool? ConsumoEnviadoSap,
    bool? EnviadoSap,
    bool Borrado,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
