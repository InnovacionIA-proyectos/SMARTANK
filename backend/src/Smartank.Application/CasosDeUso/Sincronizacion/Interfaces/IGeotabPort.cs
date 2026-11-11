using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>
/// Cliente de solo lectura de la API de Geotab (JSON-RPC). Sin credenciales lanza GEOTAB_NO_CONFIGURADO al usarse; si el
/// servicio no responde, GEOTAB_NO_DISPONIBLE.
/// </summary>
public interface IGeotabPort
{
    /// <summary>Todos los dispositivos (para mapear device.id a placa).</summary>
    Task<IReadOnlyList<clsGeotabDispositivoDto>> ListarDispositivosAsync(CancellationToken ct = default);

    /// <summary>Posicion actual de toda la flota (DeviceStatusInfo, una sola llamada).</summary>
    Task<IReadOnlyList<clsGeotabEstadoDto>> ObtenerEstadoDispositivosAsync(CancellationToken ct = default);

    /// <summary>Lecturas del nivel de combustible en porcentaje (DiagnosticFuelLevelId) desde la fecha.</summary>
    Task<IReadOnlyList<clsGeotabLecturaDto>> ObtenerNivelCombustibleAsync(DateTimeOffset desde, CancellationToken ct = default);

    /// <summary>Combustible total usado en litros, contador acumulado (DiagnosticDeviceTotalFuelId), desde la fecha.</summary>
    Task<IReadOnlyList<clsGeotabLecturaDto>> ObtenerCombustibleUsadoAsync(DateTimeOffset desde, CancellationToken ct = default);

    /// <summary>Odometro en metros, contador acumulado (DiagnosticOdometerId), desde la fecha.</summary>
    Task<IReadOnlyList<clsGeotabLecturaDto>> ObtenerOdometroAsync(DateTimeOffset desde, CancellationToken ct = default);
}
