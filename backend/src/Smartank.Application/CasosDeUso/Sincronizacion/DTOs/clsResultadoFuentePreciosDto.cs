namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Resultado de la sincronizacion de precios de una red: EDS de la fuente, actualizadas y creadas.</summary>
public sealed record clsResultadoFuentePreciosDto(int Total, int Actualizadas, int Creadas);
