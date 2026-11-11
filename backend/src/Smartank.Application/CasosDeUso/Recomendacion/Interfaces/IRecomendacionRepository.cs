using Smartank.Domain.Estaciones.Entidades;

namespace Smartank.Application.CasosDeUso.Recomendacion.Interfaces;

/// <summary>Lecturas propias de la recomendacion de tanqueo (sin seguimiento ni efectos).</summary>
public interface IRecomendacionRepository
{
    /// <summary>
    /// Estaciones con precio de ACPM y coordenadas (la flota es diesel: las que no venden ACPM no sirven para tanquear), en un
    /// orden estable (ruta y precio) para que los empates de precio se resuelvan siempre igual.
    /// </summary>
    Task<IReadOnlyList<clsEstacion>> ListarEstacionesConPrecioAcpmAsync(CancellationToken ct);

    /// <summary>Polilinea [[lat,lng],...] de la ruta del viaje, o null si el viaje no tiene ruta calculada.</summary>
    Task<double[][]?> ObtenerPolilineaDeViajeAsync(Guid viajeId, CancellationToken ct);
}
