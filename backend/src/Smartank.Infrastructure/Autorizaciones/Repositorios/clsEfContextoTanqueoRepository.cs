using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Domain.Conductores.Entidades;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Autorizaciones.Repositorios;

public sealed class clsEfContextoTanqueoRepository(clsSmartankDbContext db) : IContextoTanqueoRepository
{
    // El viaje va SEGUIDO: la autonomia se persiste sobre la entidad con IUnitOfWork.
    public Task<clsViaje?> ObtenerViajeAsync(Guid id, CancellationToken ct) =>
        db.Viajes.FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<clsVehiculo?> ObtenerVehiculoAsync(Guid id, CancellationToken ct) =>
        db.Vehiculos.AsNoTracking().FirstOrDefaultAsync(v => v.Id == id, ct);

    public Task<clsEstacion?> ObtenerEstacionAsync(Guid id, CancellationToken ct) =>
        db.Estaciones.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id, ct);

    public Task<clsConductor?> ObtenerConductorPorCedulaAsync(string cedula, CancellationToken ct) =>
        db.Conductores.AsNoTracking().FirstOrDefaultAsync(c => c.Cedula == cedula, ct);
}
