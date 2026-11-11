namespace Smartank.Api.Sincronizacion;

/// <summary>
/// Cuerpo opcional de <c>POST /api/estaciones/precios/sync</c>. Con <c>precios</c> (al menos uno) se hace la carga manual; sin
/// cuerpo, o sin precios, se consultan las fuentes reales (Terpel y Distracom). <c>ruta</c> se acepta por compatibilidad con
/// el contrato original y no tiene efecto.
/// </summary>
public sealed record clsSincronizarPreciosRequest(string? Ruta, IReadOnlyList<clsPrecioManualRequest>? Precios);
