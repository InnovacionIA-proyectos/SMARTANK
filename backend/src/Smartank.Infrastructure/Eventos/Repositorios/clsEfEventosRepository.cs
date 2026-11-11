using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Eventos.Repositorios;

public sealed class clsEfEventosRepository(clsSmartankDbContext db) : IEventosRepository
{
    public Task<clsEventoTanqueo?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.EventosTanqueo.FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<bool> ExistePorAutorizacionAsync(Guid autorizacionId, CancellationToken ct) =>
        db.EventosTanqueo.AnyAsync(e => e.AutorizacionId == autorizacionId, ct);

    public void Agregar(clsEventoTanqueo evento) => db.EventosTanqueo.Add(evento);

    public void AgregarAlerta(clsAlerta alerta) => db.Alertas.Add(alerta);

    public Task<decimal?> ObtenerPrecioMinimoDeRutaAsync(string ruta, CancellationToken ct) =>
        db.Estaciones.AsNoTracking()
            .Where(e => e.Ruta == ruta && e.PrecioGalonActual != null)
            .MinAsync(e => e.PrecioGalonActual, ct);
}
