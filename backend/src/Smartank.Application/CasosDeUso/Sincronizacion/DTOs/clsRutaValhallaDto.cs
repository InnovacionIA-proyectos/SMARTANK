namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>
/// Calculo de ruta de Valhalla con su geometria COMPLETA en WKT (la simplificacion la hace el caso de uso, no SQL Server),
/// kilometros, numero de puntos original y la respuesta del servicio de ruteo como texto JSON.
/// </summary>
public sealed record clsRutaValhallaDto(
    long ValhallaId,
    string Fo,
    double? Kilometros,
    int NumPuntosOriginal,
    string? Wkt,
    string? DetalleJson,
    DateTimeOffset? FechaCalculo);
