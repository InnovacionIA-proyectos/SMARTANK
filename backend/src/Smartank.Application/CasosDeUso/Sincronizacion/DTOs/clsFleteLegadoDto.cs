namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>
/// Flete (FO) de tblFOs traducido a nombres propios. <see cref="NumeroFo"/> ya viene sin ceros a la izquierda y
/// <see cref="FechaDespacho"/> ya esta en UTC (el legado la guarda en hora de Colombia). Incluye los borrados.
/// </summary>
public sealed record clsFleteLegadoDto(
    long FoId,
    string PlacaCabezote,
    string? ProgramaCabezote,
    string? PlacaTrailer,
    string? CedulaConductor,
    string? Transportista,
    string? CodigoRuta,
    string? NombreRuta,
    string? CiudadOrigen,
    string? CiudadDestino,
    string? NumeroFo,
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
