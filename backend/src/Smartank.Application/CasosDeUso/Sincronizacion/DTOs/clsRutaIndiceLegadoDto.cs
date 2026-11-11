namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Indice ligero de Valhalla: el calculo mas reciente (ID mayor, operacion normal) de cada FO. Fo ya viene sin ceros.</summary>
public sealed record clsRutaIndiceLegadoDto(string Fo, long ValhallaId);
