using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Eventos.Repositorios;

/// <summary>
/// Historial de tanqueos combinado (port de listHistorialTanqueos del origen): parte del historial REAL (tblTanqueos) y le
/// adjunta, si existe, la autorizacion de la app y su bitacora. El enlace con la autorizacion es heuristico: mismo vehiculo
/// (placa), fecha del tanqueo dentro de la ventana de la autorizacion mas o menos 12 h y estacion coincidente por nombre.
/// </summary>
public sealed class clsEfEventosConsultas(clsSmartankDbContext db) : IEventosConsultas
{
    private const int MAX_FILAS = 2000;
    private const int HORAS_HOLGURA_VENTANA = 12;

    public async Task<IReadOnlyList<clsHistorialTanqueoFila>> ListarHistorialAsync(clsFiltroHistorial filtro, CancellationToken ct)
    {
        var tanqueos = db.Tanqueos.AsNoTracking().AsQueryable();
        if (filtro.Placa is { } placa) tanqueos = tanqueos.Where(t => t.Placa.Contains(placa));
        if (filtro.Proveedor is { } proveedor) tanqueos = tanqueos.Where(t => t.Fuente == proveedor);
        if (filtro.Desde is { } desde)
        {
            var inicio = new DateTimeOffset(desde.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            tanqueos = tanqueos.Where(t => t.Fecha >= inicio);
        }
        if (filtro.Hasta is { } hasta)
        {
            // Rango inclusivo: hasta el final del dia (exclusivo el inicio del dia siguiente).
            var finExclusivo = new DateTimeOffset(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            tanqueos = tanqueos.Where(t => t.Fecha < finExclusivo);
        }

        // La placa se compara sin distinguir mayusculas (collation CI de la base). La autorizacion enlazada es la de la ventana
        // que contiene el tanqueo y cuyo inicio queda mas cerca de la fecha del tanqueo.
        var filas = await (
            from t in tanqueos
            join ve in db.Vehiculos on t.Placa equals ve.Placa into vehiculos
            from ve in vehiculos.DefaultIfEmpty()
            orderby t.Fecha descending
            select new
            {
                t.Id,
                t.Fuente,
                t.Placa,
                TipoMotor = ve.TipoMotor,
                Programa = ve.Programa,
                t.Estacion,
                t.Ciudad,
                t.Fecha,
                t.Galones,
                t.ValorGalon,
                t.Kilometraje,
                Autorizacion = (
                    from a in db.AutorizacionesTanqueo
                    join es in db.Estaciones on a.EstacionId equals es.Id
                    where a.VehiculoId == ve.Id
                        && t.Fecha >= a.FechaInicio.AddHours(-HORAS_HOLGURA_VENTANA)
                        && t.Fecha <= a.FechaFin.AddHours(HORAS_HOLGURA_VENTANA)
                        && es.NombreNormalizado == t.Estacion!.Trim()
                    orderby Math.Abs(EF.Functions.DateDiffSecond(a.FechaInicio, t.Fecha))
                    select new { a.Id, a.PrecioTecho }).FirstOrDefault(),
            })
            .Take(MAX_FILAS)
            .ToListAsync(ct);

        // La bitacora (evento) de las autorizaciones enlazadas se lee aparte, en una sola consulta.
        var autorizacionIds = filas.Where(f => f.Autorizacion is not null).Select(f => f.Autorizacion!.Id).Distinct().ToList();
        var bitacoras = autorizacionIds.Count == 0
            ? []
            : await db.EventosTanqueo.AsNoTracking()
                .Where(e => autorizacionIds.Contains(e.AutorizacionId))
                .Select(e => new { e.AutorizacionId, e.FotoSurtidorUrl, e.NivelAntes, e.NivelDespues })
                .ToDictionaryAsync(e => e.AutorizacionId, ct);

        return filas.Select(f =>
        {
            var bitacora = f.Autorizacion is { } a && bitacoras.TryGetValue(a.Id, out var b) ? b : null;
            return new clsHistorialTanqueoFila(
                f.Id,
                f.Fuente,
                f.Placa,
                f.TipoMotor,
                f.Programa,
                f.Estacion,
                f.Ciudad,
                f.Fecha,
                f.Galones,
                f.ValorGalon,
                f.Kilometraje,
                f.Autorizacion is not null,
                f.Autorizacion?.PrecioTecho,
                bitacora?.FotoSurtidorUrl,
                bitacora?.NivelAntes,
                bitacora?.NivelDespues);
        }).ToList();
    }
}
