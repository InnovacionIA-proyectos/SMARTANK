namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Estacion del catalogo propio reducida a lo necesario para cruzarla con la que reporta la fuente de precios.</summary>
public sealed record clsEstacionReferencia(Guid Id, string Nombre, double? Lat, double? Lng, string? CodigoExterno);
