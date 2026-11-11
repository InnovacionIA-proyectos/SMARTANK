using Smartank.Application.CasosDeUso.Viajes.DTOs;

namespace Smartank.Application.CasosDeUso.Viajes.Interfaces;

/// <summary>Consultas de solo lectura de viajes para los paneles (con joins a vehiculo y ruta).</summary>
public interface IViajesLecturaRepository
{
    /// <summary>Viajes no cerrados que cumplen el filtro, del mas reciente al mas antiguo (por fecha de creacion).</summary>
    Task<IReadOnlyList<clsViajeListadoFila>> ListarAsync(clsViajesFiltro filtro, CancellationToken ct);

    /// <summary>Rutas y anios presentes entre los viajes no cerrados, para poblar los filtros del panel.</summary>
    Task<clsViajesOpcionesDto> ObtenerOpcionesAsync(CancellationToken ct);

    /// <summary>Viaje con su polilinea y la posicion del vehiculo, o null si el viaje no existe.</summary>
    Task<clsViajeRutaFila?> ObtenerRutaAsync(Guid viajeId, CancellationToken ct);
}
