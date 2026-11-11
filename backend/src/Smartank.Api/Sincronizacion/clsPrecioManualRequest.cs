namespace Smartank.Api.Sincronizacion;

/// <summary>Precio por galon (COP, no negativo) que se carga a mano para una estacion.</summary>
public sealed record clsPrecioManualRequest(Guid EstacionId, decimal PrecioGalonActual);
