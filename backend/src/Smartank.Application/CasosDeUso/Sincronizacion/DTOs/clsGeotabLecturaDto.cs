namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Lectura de un diagnostico (StatusData de Geotab): nivel en %, combustible usado en litros u odometro en metros segun el diagnostico consultado.</summary>
public sealed record clsGeotabLecturaDto(string DispositivoId, double Valor, DateTimeOffset FechaUtc);
