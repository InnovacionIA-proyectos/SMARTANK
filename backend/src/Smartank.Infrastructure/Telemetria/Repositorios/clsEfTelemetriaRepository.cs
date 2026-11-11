using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Telemetria.Interfaces;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Telemetria.Repositorios;

public sealed class clsEfTelemetriaRepository(clsSmartankDbContext db) : ITelemetriaRepository
{
    public Task<clsVehiculo?> ObtenerVehiculoAsync(Guid id, CancellationToken ct) =>
        db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<clsVehiculo?> ActualizarTelemetriaAsync(
        string placa,
        decimal nivelGalones,
        double? odometro,
        double? lat,
        double? lng,
        DateTimeOffset leidoAt,
        CancellationToken ct)
    {
        // Un solo UPDATE (atomico, sin leer-modificar-escribir): las lecturas concurrentes de dos vehiculos distintos, o
        // de uno solo, nunca se pisan a medias. Lo que no viene (odometro, posicion) conserva su valor anterior.
        var filas = await db.Vehiculos
            .Where(v => v.Placa == placa)
            .ExecuteUpdateAsync(s => s
                .SetProperty(v => v.NivelTanqueActual, nivelGalones)
                .SetProperty(v => v.NivelAt, leidoAt)
                .SetProperty(v => v.Odometro, v => odometro ?? v.Odometro)
                .SetProperty(v => v.UltimaLat, v => lat ?? v.UltimaLat)
                .SetProperty(v => v.UltimaLng, v => lng ?? v.UltimaLng)
                .SetProperty(v => v.UbicacionAt, v => lat != null ? leidoAt : v.UbicacionAt), ct);

        if (filas == 0) return null;

        return await db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Placa == placa, ct);
    }
}
