namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Precio por galon que un administrador carga a mano para una EDS (respaldo cuando las fuentes automaticas no sirven).</summary>
public sealed record clsPrecioManualItemDto(Guid EstacionId, decimal PrecioGalonActual);
