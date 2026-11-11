namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Error de un registro puntual durante una sincronizacion: su clave (placa, FO, cedula...) y el mensaje.</summary>
public sealed record clsErrorSincronizacion(string Clave, string Mensaje);
