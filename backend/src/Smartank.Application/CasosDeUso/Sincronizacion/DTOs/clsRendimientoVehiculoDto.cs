namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Rendimiento historico (km/galon) derivado de Geotab para un vehiculo.</summary>
public sealed record clsRendimientoVehiculoDto(Guid VehiculoId, double KmPorGalon);
