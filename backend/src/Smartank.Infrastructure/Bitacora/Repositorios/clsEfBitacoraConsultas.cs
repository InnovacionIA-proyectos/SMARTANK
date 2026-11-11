using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Bitacora.DTOs;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Bitacora.Repositorios;

public sealed class clsEfBitacoraConsultas(clsSmartankDbContext db) : IBitacoraConsultas
{
    public Task<clsBitacoraDto?> ObtenerAsync(Guid autorizacionId, CancellationToken ct) =>
        (from a in db.AutorizacionesTanqueo.AsNoTracking()
         where a.Id == autorizacionId
         join e in db.Estaciones on a.EstacionId equals e.Id
         join ve in db.Vehiculos on a.VehiculoId equals ve.Id
         join v in db.Viajes on a.ViajeId equals v.Id
         select new clsBitacoraDto(
             a.Id,
             ve.Placa,
             v.Manifiesto,
             v.Ruta,
             e.Nombre,
             a.GalonesMax,
             a.Estado,
             db.EventosTanqueo.Any(ev => ev.AutorizacionId == a.Id),
             a.FechaFin))
        .FirstOrDefaultAsync(ct);
}
