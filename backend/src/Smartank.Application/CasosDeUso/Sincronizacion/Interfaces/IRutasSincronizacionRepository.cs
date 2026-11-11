using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Escritura masiva de rutas (polilineas) sincronizadas.</summary>
public interface IRutasSincronizacionRepository
{
    /// <summary>Upsert por FO. Origen y destino de la ruta los deriva la base (trigger) de la polilinea.</summary>
    Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsRutaUpsertDto> rutas, CancellationToken ct = default);
}
