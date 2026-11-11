using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Lectura (solo lectura) del historial de tanqueos de Distracom y Terpel del SQL Server legado.</summary>
public interface ITanqueosLegadoPort
{
    /// <summary>Tanqueos de Distracom desde la fecha (FECHAFINAL, inclusive, hora de Colombia).</summary>
    Task<IReadOnlyList<clsTanqueoLegadoDto>> ListarDistracomAsync(DateTime desdeHoraColombia, CancellationToken ct = default);

    /// <summary>Tanqueos de Terpel desde la fecha (FECHATRANSACCION, inclusive, hora de Colombia).</summary>
    Task<IReadOnlyList<clsTanqueoLegadoDto>> ListarTerpelAsync(DateTime desdeHoraColombia, CancellationToken ct = default);
}
