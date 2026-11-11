using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Application.CasosDeUso.Telemetria.Interfaces;

public interface ITelemetriaRepository
{
    /// <summary>Vehiculo por id (sin seguimiento de cambios), o null si no existe.</summary>
    Task<clsVehiculo?> ObtenerVehiculoAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Escribe de forma atomica (un solo UPDATE) la lectura de nivel y, si vienen, odometro y posicion; los datos que no
    /// vienen conservan su valor anterior. <paramref name="leidoAt"/> queda como fecha de la lectura de nivel y de la posicion.
    /// Devuelve el vehiculo ya actualizado, o null si no existe un vehiculo con esa placa (la placa se compara sin distinguir mayusculas).
    /// </summary>
    Task<clsVehiculo?> ActualizarTelemetriaAsync(
        string placa,
        decimal nivelGalones,
        double? odometro,
        double? lat,
        double? lng,
        DateTimeOffset leidoAt,
        CancellationToken ct);
}
