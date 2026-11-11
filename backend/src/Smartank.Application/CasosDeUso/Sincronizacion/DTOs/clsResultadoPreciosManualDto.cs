namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Resultado de la carga manual de precios con la forma del contrato original (<c>{ modo: "manual", actualizadas, estaciones }</c>).</summary>
public sealed record clsResultadoPreciosManualDto(string Modo, int Actualizadas, IReadOnlyList<clsEstacionPrecioDto> Estaciones);
