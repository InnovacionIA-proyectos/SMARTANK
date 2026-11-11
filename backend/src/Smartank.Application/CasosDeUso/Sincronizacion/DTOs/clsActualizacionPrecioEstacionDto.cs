namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Nuevos precios de una EDS existente. El precio por galon actual pasa a ser el ACPM (o, sin el, la corriente).</summary>
public sealed record clsActualizacionPrecioEstacionDto(Guid EstacionId, decimal? Acpm, decimal? Corriente);
