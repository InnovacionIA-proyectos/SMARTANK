using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Lectura de rutas con polilinea y escritura masiva del precio de referencia por viaje (tblViajePreciosRef).</summary>
public interface IPreciosRutaRepository
{
    /// <summary>
    /// Rutas con viaje y polilinea de al menos 2 puntos. Se entrega en flujo (no se materializan miles de polilineas en
    /// memoria a la vez).
    /// </summary>
    IAsyncEnumerable<clsRutaParaPrecioDto> ListarRutasConPolilineaAsync(CancellationToken ct = default);

    /// <summary>Upsert por viaje; refresca la fecha de calculo.</summary>
    Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsPrecioReferenciaViajeDto> precios, CancellationToken ct = default);
}
