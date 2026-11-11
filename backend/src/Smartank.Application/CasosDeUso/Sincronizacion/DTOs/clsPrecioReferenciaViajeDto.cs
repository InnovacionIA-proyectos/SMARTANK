namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Precio de ACPM de referencia de un viaje (fila de tblViajePreciosRef). Min y max nulos con EdsCerca = 0.</summary>
public sealed record clsPrecioReferenciaViajeDto(Guid ViajeId, decimal? AcpmMin, decimal? AcpmMax, int EdsCerca);
