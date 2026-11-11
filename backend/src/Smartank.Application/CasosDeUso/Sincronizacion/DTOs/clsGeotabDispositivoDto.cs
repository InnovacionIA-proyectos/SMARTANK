namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Dispositivo de Geotab. El enlace con el vehiculo es por placa: <see cref="Nombre"/> (o, si falta, <see cref="Placa"/>) es la placa.</summary>
public sealed record clsGeotabDispositivoDto(string Id, string? Nombre, string? Placa);
