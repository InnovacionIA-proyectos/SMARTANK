namespace Smartank.Application.CasosDeUso.Eventos.DTOs;

/// <summary>Evento de tanqueo (bitacora del conductor) tal como lo expone el API (serializeEvento del origen).</summary>
public sealed record clsEventoDto(
    Guid Id,
    Guid AutorizacionId,
    Guid VehiculoId,
    Guid EstacionId,
    decimal GalonesCargados,
    decimal? PrecioPagado,
    string? FotoSurtidorUrl,
    double? OdometroEvento,
    decimal? NivelAntes,
    decimal? NivelDespues,
    Guid? RegistradoPor,
    string? Programa,
    string? Producto,
    DateTimeOffset OcurridoAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
