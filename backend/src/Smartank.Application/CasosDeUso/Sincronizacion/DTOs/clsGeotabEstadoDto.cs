namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Posicion actual de un dispositivo (DeviceStatusInfo de Geotab).</summary>
public sealed record clsGeotabEstadoDto(string DispositivoId, double? Latitud, double? Longitud, DateTimeOffset? FechaUtc);
