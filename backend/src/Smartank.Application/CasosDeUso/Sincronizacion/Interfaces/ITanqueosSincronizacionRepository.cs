using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Escritura masiva del historial de tanqueos.</summary>
public interface ITanqueosSincronizacionRepository
{
    /// <summary>Upsert por (Fuente, FuenteId).</summary>
    Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsTanqueoUpsertDto> tanqueos, CancellationToken ct = default);
}
