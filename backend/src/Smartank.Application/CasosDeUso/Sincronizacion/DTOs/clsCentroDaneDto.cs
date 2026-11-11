namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Centro poblado DANE con coordenadas; <see cref="Codigo"/> normalizado a 12 digitos (clave de cruce con las ciudades del FO).</summary>
public sealed record clsCentroDaneDto(string Codigo, double Lat, double Lng);
