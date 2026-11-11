using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Estaciones.Interfaces;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Estaciones.Repositorios;

public sealed class clsEfEstacionesRepository(clsSmartankDbContext db) : IEstacionesRepository
{
    public async Task<IReadOnlyList<clsEstacion>> ListarAsync(string? ruta, CancellationToken ct)
    {
        var consulta = db.Estaciones.AsNoTracking();

        // SQL Server ordena los NULL primero; el contrato pide las que no tienen precio (o ruta) al final, por eso el
        // "== null" explicito antes de cada columna. El nombre desempata para que el orden sea estable.
        if (ruta is not null)
        {
            return await consulta
                .Where(e => e.Ruta == ruta)
                .OrderBy(e => e.PrecioGalonActual == null)
                .ThenBy(e => e.PrecioGalonActual)
                .ThenBy(e => e.Nombre)
                .ToListAsync(ct);
        }

        return await consulta
            .OrderBy(e => e.Ruta == null)
            .ThenBy(e => e.Ruta)
            .ThenBy(e => e.PrecioGalonActual == null)
            .ThenBy(e => e.PrecioGalonActual)
            .ThenBy(e => e.Nombre)
            .ToListAsync(ct);
    }
}
