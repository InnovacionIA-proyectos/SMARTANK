using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

/// <summary>Ejecuta UN job de sincronizacion por su nombre (ver <see cref="clsNombresJob"/>) y devuelve sus contadores. Lanza lo que lance el caso de uso.</summary>
public interface IEjecutorJob
{
    Task<clsResultadoSincronizacion> EjecutarAsync(string nombreJob, CancellationToken ct = default);
}
