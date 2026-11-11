using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Escrituras masivas y lecturas de vehiculos para las sincronizaciones. Cada escritura es idempotente y aisla las filas con error.</summary>
public interface IVehiculosSincronizacionRepository
{
    /// <summary>Upsert por placa del maestro; conserva telemetria y parametros de negocio de la aplicacion.</summary>
    Task<clsResultadoUpsertDto> UpsertDesdeLegadoAsync(IReadOnlyList<clsCabezoteLegadoDto> vehiculos, CancellationToken ct = default);

    /// <summary>Mapa placa (sin distinguir mayusculas) a id de todos los vehiculos.</summary>
    Task<IReadOnlyDictionary<string, Guid>> ObtenerMapaPlacaAIdAsync(CancellationToken ct = default);

    /// <summary>Id, placa y capacidad de tanque de todos los vehiculos.</summary>
    Task<IReadOnlyList<clsVehiculoBasicoDto>> ListarBasicosAsync(CancellationToken ct = default);

    /// <summary>Actualiza ubicacion, nivel, odometro y enlace con el dispositivo Geotab; un campo nulo conserva el valor almacenado.</summary>
    Task<clsResultadoUpsertDto> ActualizarTelemetriaAsync(IReadOnlyList<clsActualizacionTelemetriaDto> telemetria, CancellationToken ct = default);

    /// <summary>Actualiza el rendimiento historico (km/galon).</summary>
    Task<clsResultadoUpsertDto> ActualizarRendimientoAsync(IReadOnlyList<clsRendimientoVehiculoDto> rendimientos, CancellationToken ct = default);
}
