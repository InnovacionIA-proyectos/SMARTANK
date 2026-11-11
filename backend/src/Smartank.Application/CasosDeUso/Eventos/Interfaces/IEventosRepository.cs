using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Eventos.Entidades;

namespace Smartank.Application.CasosDeUso.Eventos.Interfaces;

/// <summary>
/// Acceso a los eventos de bitacora y a las alertas que genera su verificacion. Los eventos devueltos estan seguidos por el
/// contexto; <see cref="Agregar"/> y <see cref="AgregarAlerta"/> solo registran la fila (se confirma con <c>IUnitOfWork</c>).
/// </summary>
public interface IEventosRepository
{
    Task<clsEventoTanqueo?> ObtenerAsync(Guid id, CancellationToken ct);

    /// <summary>True si la autorizacion ya tiene su evento de bitacora (la base permite uno solo por autorizacion).</summary>
    Task<bool> ExistePorAutorizacionAsync(Guid autorizacionId, CancellationToken ct);

    void Agregar(clsEventoTanqueo evento);

    void AgregarAlerta(clsAlerta alerta);

    /// <summary>Precio mas bajo de galon entre las estaciones de la ruta (referencia de "estacion cara"); null si ninguna tiene precio.</summary>
    Task<decimal?> ObtenerPrecioMinimoDeRutaAsync(string ruta, CancellationToken ct);
}
