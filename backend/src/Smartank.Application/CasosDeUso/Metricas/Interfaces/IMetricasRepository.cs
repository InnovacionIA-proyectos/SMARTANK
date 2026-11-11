using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Domain.Metricas.Reglas;

namespace Smartank.Application.CasosDeUso.Metricas.Interfaces;

public interface IMetricasRepository
{
    /// <summary>
    /// Tanqueos REALES de ACPM/diesel que cumplen los filtros, cada uno ligado a su viaje (el vigente del vehiculo en esa
    /// fecha: iniciado hasta 30 dias antes del tanqueo), a la referencia de precio de la ruta de ese viaje y, si se puede
    /// casar por nombre, a la estacion del catalogo. Solo lee de la base propia.
    /// </summary>
    Task<IReadOnlyList<clsTanqueoMetrica>> ListarTanqueosAsync(clsRecuperacionFiltrosDto filtros, CancellationToken ct);

    /// <summary>Valores distintos de programas, proveedores, productos, anios, meses y rutas, sin aplicar filtros.</summary>
    Task<clsOpcionesFiltroDto> ObtenerOpcionesAsync(CancellationToken ct);
}
