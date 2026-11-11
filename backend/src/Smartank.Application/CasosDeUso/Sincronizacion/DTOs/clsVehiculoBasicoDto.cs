namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Vehiculo de la flota reducido a lo que necesitan la telemetria y el rendimiento.</summary>
public sealed record clsVehiculoBasicoDto(Guid Id, string Placa, decimal? CapacidadTanqueGal);
