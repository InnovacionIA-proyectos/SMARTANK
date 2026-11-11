namespace Smartank.Application.CasosDeUso.Recomendacion.DTOs;

/// <summary>Estacion de servicio (EDS) elegida, tal como la expone el API (serializeEstacion del origen).</summary>
public sealed record clsEstacionElegidaDto(
    Guid Id,
    string Nombre,
    string? Ruta,
    double? Lat,
    double? Lng,
    decimal? PrecioGalonActual,
    decimal? PrecioAcpm,
    decimal? PrecioCorriente,
    DateTimeOffset? PrecioActualizadoAt,
    bool SoportaPreautorizacion,
    string? Proveedor,
    string? Producto,
    string? CodigoExterno,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
