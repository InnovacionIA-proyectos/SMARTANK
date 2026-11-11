using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Recomendacion.Interfaces;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Recomendacion.Repositorios;

public sealed class clsEfRecomendacionRepository(clsSmartankDbContext db) : IRecomendacionRepository
{
    public async Task<IReadOnlyList<clsEstacion>> ListarEstacionesConPrecioAcpmAsync(CancellationToken ct) =>
        // Orden estable (ruta, precio, id) para que los empates de precio se resuelvan siempre igual. Las marcas "es nulo"
        // reproducen NULLS LAST del origen (SQL Server ordena los nulos primero).
        await db.Estaciones.AsNoTracking()
            .Where(e => e.PrecioAcpm != null && e.Lat != null && e.Lng != null)
            .OrderBy(e => e.Ruta == null).ThenBy(e => e.Ruta)
            .ThenBy(e => e.PrecioGalonActual == null).ThenBy(e => e.PrecioGalonActual)
            .ThenBy(e => e.Id)
            .ToListAsync(ct);

    public Task<double[][]?> ObtenerPolilineaDeViajeAsync(Guid viajeId, CancellationToken ct) =>
        // Se proyecta solo la columna de la polilinea: la ruta no se trae completa.
        db.Rutas.AsNoTracking().Where(r => r.ViajeId == viajeId).Select(r => r.Polilinea).FirstOrDefaultAsync(ct);
}
