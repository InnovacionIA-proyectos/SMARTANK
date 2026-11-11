using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Autorizaciones.Repositorios;

/// <summary>
/// Autorizaciones sobre el DbContext de la peticion. Devuelve entidades SEGUIDAS: el rowversion que EF lee junto con la fila
/// protege la transicion de estado (un cambio concurrente hace fallar el guardado con CONFLICTO_CONCURRENCIA).
/// </summary>
public sealed class clsEfAutorizacionesRepository(clsSmartankDbContext db) : IAutorizacionesRepository
{
    public Task<clsAutorizacionTanqueo?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.AutorizacionesTanqueo.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<clsAutorizacionTanqueo?> ObtenerPropuestaDeViajeAsync(Guid viajeId, CancellationToken ct) =>
        db.AutorizacionesTanqueo.FirstOrDefaultAsync(a => a.ViajeId == viajeId && a.Estado == EstadoAutorizacion.Propuesta, ct);

    public void Agregar(clsAutorizacionTanqueo autorizacion) => db.AutorizacionesTanqueo.Add(autorizacion);

    public void Descartar(clsAutorizacionTanqueo autorizacion) => db.Entry(autorizacion).State = EntityState.Detached;
}
