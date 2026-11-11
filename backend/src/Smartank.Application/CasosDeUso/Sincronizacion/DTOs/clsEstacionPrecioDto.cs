namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Estacion tal como la devuelve la carga manual de precios (mismos campos que la estacion del contrato original).</summary>
public sealed record clsEstacionPrecioDto(
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
