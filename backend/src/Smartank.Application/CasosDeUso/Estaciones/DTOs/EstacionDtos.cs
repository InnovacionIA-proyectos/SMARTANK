namespace Smartank.Application.CasosDeUso.Estaciones.DTOs;

/// <summary>Estacion de servicio (EDS) del catalogo con ubicacion y precios; misma forma que el serializador del origen.</summary>
public record clsEstacionDto(
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

/// <summary>Respuesta de GET /api/estaciones: { ruta, total, estaciones }. <c>Ruta</c> repite el filtro aplicado (null si no hubo).</summary>
public record clsEstacionesResponse(string? Ruta, int Total, IReadOnlyList<clsEstacionDto> Estaciones);
