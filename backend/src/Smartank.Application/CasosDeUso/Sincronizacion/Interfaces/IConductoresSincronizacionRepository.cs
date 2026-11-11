using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Escritura masiva de conductores sincronizados desde el legado.</summary>
public interface IConductoresSincronizacionRepository
{
    /// <summary>Upsert por cedula. El celular se sobrescribe en cada sincronizacion (la app movil es la fuente de verdad; null si no hay uno valido).</summary>
    Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsConductorUpsertDto> conductores, CancellationToken ct = default);
}
