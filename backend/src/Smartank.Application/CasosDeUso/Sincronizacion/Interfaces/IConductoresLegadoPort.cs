using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Lectura (solo lectura) de conductores y de los celulares de la app movil en el SQL Server legado.</summary>
public interface IConductoresLegadoPort
{
    /// <summary>Todos los conductores (activos e inactivos), ordenados por nombre.</summary>
    Task<IReadOnlyList<clsConductorLegadoDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>
    /// Celular por documento (cedula) tomado de la app movil: por documento el registro Activo y, entre ellos, el de ingreso
    /// mas reciente. El valor es el texto crudo del legado (normalizarlo es responsabilidad del caso de uso).
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> ListarCelularesAplMovilAsync(CancellationToken ct = default);
}
