using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Lectura (solo lectura) de los fletes (FOs) del SQL Server legado.</summary>
public interface IFletesLegadoPort
{
    /// <summary>
    /// Fletes despachados desde <paramref name="desdeHoraColombia"/> (inclusive), borrados incluidos, ordenados por despacho.
    /// El parametro es hora de pared de Colombia, igual que la columna contra la que se compara.
    /// </summary>
    Task<IReadOnlyList<clsFleteLegadoDto>> ListarDesdeAsync(DateTime desdeHoraColombia, CancellationToken ct = default);
}
