using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Viaje listo para escribir (clave FoId): el estado ya esta derivado y el vehiculo resuelto por placa.</summary>
public sealed record clsViajeUpsertDto(
    long FoId,
    Guid VehiculoId,
    string Ruta,
    EstadoViaje Estado,
    string? Manifiesto,
    DateTimeOffset? InicioAt,
    string PlacaCabezote,
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
    decimal? KmtsRuta,
    DateTimeOffset? FechaDespacho,
    bool GastosGenerados,
    bool GastosEnviadoSap,
    bool ConsumoEnviadoSap,
    bool EnviadoSap,
    bool Borrado);
