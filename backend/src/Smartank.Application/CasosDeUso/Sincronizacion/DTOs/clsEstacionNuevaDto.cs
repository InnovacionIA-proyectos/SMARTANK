namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>EDS que la fuente de precios reporta y el catalogo aun no tiene; se crea con soporte de preautorizacion.</summary>
public sealed record clsEstacionNuevaDto(
    string Nombre,
    double? Lat,
    double? Lng,
    string Proveedor,
    string? CodigoExterno,
    decimal? Acpm,
    decimal? Corriente,
    string? Producto);
