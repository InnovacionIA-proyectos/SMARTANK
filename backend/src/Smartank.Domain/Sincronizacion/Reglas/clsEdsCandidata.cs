namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Estacion con coordenadas y precio de ACPM, candidata a estar cerca de una ruta.</summary>
public readonly record struct clsEdsCandidata(double Lat, double Lng, decimal PrecioAcpm);
