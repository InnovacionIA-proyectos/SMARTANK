using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Enums;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Alertas.Repositorios;

public sealed class clsEfAlertasRepository(clsSmartankDbContext db) : IAlertasRepository
{
    public async Task<IReadOnlyList<clsAlertaListadoDto>> ListarAsync(EstadoAlerta? estado, CancellationToken ct)
    {
        var consulta =
            from a in db.Alertas.AsNoTracking()
            join ve in db.Vehiculos.AsNoTracking() on a.VehiculoId equals ve.Id
            select new { a, ve.Placa };

        if (estado is { } e) consulta = consulta.Where(x => x.a.Estado == e);

        return await consulta
            .OrderByDescending(x => x.a.CreatedAt)
            .Select(x => new clsAlertaListadoDto(
                x.a.Id, x.a.Tipo, x.a.VehiculoId, x.a.EventoId, x.a.MagnitudDesviacion, x.a.Estado,
                x.a.GestionadaPor, x.a.CreatedAt, x.a.UpdatedAt, x.Placa))
            .ToListAsync(ct);
    }

    // CON seguimiento: EF compara el RowVersion leido al guardar y lanza concurrencia si otro lo cambio.
    public Task<clsAlerta?> ObtenerParaActualizarAsync(Guid id, CancellationToken ct) =>
        db.Alertas.FirstOrDefaultAsync(a => a.Id == id, ct);

    public void Agregar(clsAlerta alerta) => db.Alertas.Add(alerta);

    public Task<bool> ExisteVehiculoAsync(Guid vehiculoId, CancellationToken ct) =>
        db.Vehiculos.AnyAsync(v => v.Id == vehiculoId, ct);

    public Task<bool> ExisteEventoAsync(Guid eventoId, CancellationToken ct) =>
        db.EventosTanqueo.AnyAsync(e => e.Id == eventoId, ct);
}
