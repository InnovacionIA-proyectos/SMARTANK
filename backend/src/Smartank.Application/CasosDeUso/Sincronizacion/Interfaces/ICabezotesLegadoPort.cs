using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Lectura (solo lectura) del maestro de cabezotes del SQL Server legado. Sin cadena Fpsap lanza FPSAP_NO_CONFIGURADO al usarse.</summary>
public interface ICabezotesLegadoPort
{
    /// <summary>Cabezotes ACTIVOS ordenados por placa.</summary>
    Task<IReadOnlyList<clsCabezoteLegadoDto>> ListarActivosAsync(CancellationToken ct = default);
}
