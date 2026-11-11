using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>
/// Precio de una EDS segun la fuente publica de su red. Los precios ya pasaron la banda realista y las coordenadas
/// ya se validaron (fuera de Colombia son null). Distracom trae codigo externo (EstacionID); Terpel no.
/// </summary>
public sealed record clsPrecioEdsDto(
    FuenteTanqueo Proveedor,
    string? CodigoExterno,
    string? Nombre,
    double? Lat,
    double? Lng,
    decimal? Acpm,
    decimal? Corriente);
