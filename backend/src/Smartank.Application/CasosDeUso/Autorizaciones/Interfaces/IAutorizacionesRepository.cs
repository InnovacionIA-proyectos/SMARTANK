using Smartank.Domain.Autorizaciones.Entidades;

namespace Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

/// <summary>
/// Acceso de escritura a las autorizaciones de tanqueo. Las entidades que devuelve estan SEGUIDAS por el contexto: el caso de
/// uso las modifica y confirma con <c>IUnitOfWork</c> o <c>IAuditoriaPort</c>; el token de concurrencia (rowversion) garantiza
/// que dos usuarios no pisen la misma transicion de estado.
/// </summary>
public interface IAutorizacionesRepository
{
    Task<clsAutorizacionTanqueo?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>La propuesta (estado Propuesta) del viaje, si existe. La base permite a lo sumo una por viaje.</summary>
    Task<clsAutorizacionTanqueo?> ObtenerPropuestaDeViajeAsync(Guid viajeId, CancellationToken ct);

    void Agregar(clsAutorizacionTanqueo autorizacion);

    /// <summary>
    /// Deja de seguir una autorizacion que se intento insertar y fallo (por ejemplo, por una propuesta duplicada), para que un
    /// guardado posterior no reintente el INSERT.
    /// </summary>
    void Descartar(clsAutorizacionTanqueo autorizacion);
}
