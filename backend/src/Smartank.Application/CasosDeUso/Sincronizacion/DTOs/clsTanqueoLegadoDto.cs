using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Lectura de tanqueo del historial legado (Distracom o Terpel) unificada. <see cref="Fecha"/> esta en UTC.</summary>
public sealed record clsTanqueoLegadoDto(
    FuenteTanqueo Fuente,
    long FuenteId,
    string Placa,
    DateTimeOffset Fecha,
    decimal? Galones,
    decimal? ValorGalon,
    decimal? ValorTotal,
    string? Producto,
    string? FamiliaProducto,
    string? Ciudad,
    string? Estacion,
    decimal? Kilometraje,
    string? CedulaConductor,
    string? Factura,
    string? Referencia,
    string? CentroCosto);
