using Smartank.Domain.Conductores.Entidades;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

/// <summary>
/// Lecturas de los datos maestros que intervienen en autonomia, recomendacion, autorizaciones y bitacora. Los viajes se
/// devuelven seguidos por el contexto (la autonomia se persiste sobre el viaje); el resto, sin seguimiento.
/// </summary>
public interface IContextoTanqueoRepository
{
    /// <summary>Viaje seguido por el contexto (el caso de uso puede actualizar su autonomia).</summary>
    Task<clsViaje?> ObtenerViajeAsync(Guid id, CancellationToken ct);

    Task<clsVehiculo?> ObtenerVehiculoAsync(Guid id, CancellationToken ct);

    Task<clsEstacion?> ObtenerEstacionAsync(Guid id, CancellationToken ct);

    Task<clsConductor?> ObtenerConductorPorCedulaAsync(string cedula, CancellationToken ct);
}
