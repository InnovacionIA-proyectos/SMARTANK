using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Lectura y escritura masiva de estaciones para la sincronizacion de precios.</summary>
public interface IEstacionesSincronizacionRepository
{
    /// <summary>Estaciones del proveedor (para cruzarlas con las de la fuente).</summary>
    Task<IReadOnlyList<clsEstacionReferencia>> ListarPorProveedorAsync(string proveedor, CancellationToken ct = default);

    /// <summary>Actualiza ACPM, corriente, precio por galon actual y fecha de actualizacion de estaciones existentes.</summary>
    Task<clsResultadoUpsertDto> ActualizarPreciosAsync(IReadOnlyList<clsActualizacionPrecioEstacionDto> precios, CancellationToken ct = default);

    /// <summary>Crea estaciones nuevas (insertados = creadas).</summary>
    Task<clsResultadoUpsertDto> InsertarAsync(IReadOnlyList<clsEstacionNuevaDto> estaciones, CancellationToken ct = default);

    /// <summary>Estaciones con coordenadas y precio de ACPM (candidatas a estar cerca de una ruta).</summary>
    Task<IReadOnlyList<clsEdsCandidata>> ListarConPrecioAcpmAsync(CancellationToken ct = default);

    /// <summary>Ids de la lista que NO existen en el catalogo.</summary>
    Task<IReadOnlyList<Guid>> ObtenerInexistentesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default);

    /// <summary>Fija el precio por galon actual de una estacion (carga manual) y la devuelve; null si no existe.</summary>
    Task<clsEstacionPrecioDto?> ActualizarPrecioGalonAsync(Guid estacionId, decimal precioGalon, CancellationToken ct = default);
}
