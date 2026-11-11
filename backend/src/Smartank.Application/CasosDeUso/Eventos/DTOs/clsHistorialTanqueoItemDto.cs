using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Eventos.DTOs;

/// <summary>Un tanqueo del historial (EventoTanqueoApi del origen).</summary>
/// <param name="Programa">Programa del vehiculo.</param>
/// <param name="EstacionNombre">Nombre de la estacion; si no viene, la ciudad; si tampoco, un guion largo.</param>
/// <param name="EstacionRuta">El historial real no trae ruta: se usa la ciudad.</param>
/// <param name="Proveedor">Red de abastecimiento (terpel o distracom).</param>
/// <param name="Autorizado">True si casa con una autorizacion de la app.</param>
/// <param name="PrecioTecho">Techo autorizado, si esta enlazado.</param>
public sealed record clsHistorialTanqueoItemDto(
    Guid Id,
    DateTimeOffset OcurridoAt,
    string Placa,
    string TipoMotor,
    string? Programa,
    string EstacionNombre,
    string? EstacionRuta,
    FuenteTanqueo Proveedor,
    decimal GalonesCargados,
    decimal? PrecioPagado,
    double? OdometroEvento,
    bool Autorizado,
    decimal? PrecioTecho,
    string? FotoSurtidorUrl,
    decimal? NivelAntes,
    decimal? NivelDespues);
