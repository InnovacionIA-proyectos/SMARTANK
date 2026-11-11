using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Viajes.DTOs;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Domain.Enums;
using Smartank.Domain.Viajes.Reglas;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Viajes.Repositorios;

public sealed class clsEfViajesLecturaRepository(clsSmartankDbContext db) : IViajesLecturaRepository
{
    public async Task<IReadOnlyList<clsViajeListadoFila>> ListarAsync(clsViajesFiltro filtro, CancellationToken ct)
    {
        var consulta =
            from v in db.Viajes.AsNoTracking()
            join ve in db.Vehiculos.AsNoTracking() on v.VehiculoId equals ve.Id
            where v.Estado != EstadoViaje.Cerrado
            select new { v, ve };

        // Todos los filtros se aplican en SQL: solo viaja lo que la pantalla muestra.
        if (filtro.Estado is { } estado) consulta = consulta.Where(x => x.v.Estado == estado);
        if (filtro.Ruta is { } ruta) consulta = consulta.Where(x => x.v.Ruta == ruta);
        if (filtro.Placa is { } placa) consulta = consulta.Where(x => x.ve.Placa.Contains(placa));
        if (filtro.Manifiesto is { } manifiesto) consulta = consulta.Where(x => x.v.Manifiesto != null && x.v.Manifiesto.Contains(manifiesto));
        if (filtro.Anio is { } anio) consulta = consulta.Where(x => x.v.FechaEfectiva != null && x.v.FechaEfectiva.Value.Year == anio);
        if (filtro.Mes is { } mes) consulta = consulta.Where(x => x.v.FechaEfectiva != null && x.v.FechaEfectiva.Value.Month == mes);

        // Vacio = manifiesto de 10 caracteres que empieza con "32" (ver clsDesplazamiento).
        var prefijoVacio = clsDesplazamiento.PREFIJO_MANIFIESTO_VACIO;
        var longitudVacio = clsDesplazamiento.LONGITUD_MANIFIESTO_VACIO;
        if (filtro.Desplazamiento == TipoDesplazamiento.Vacio)
            consulta = consulta.Where(x => x.v.Manifiesto != null && x.v.Manifiesto.StartsWith(prefijoVacio) && x.v.Manifiesto.Length == longitudVacio);
        else if (filtro.Desplazamiento == TipoDesplazamiento.Cargado)
            consulta = consulta.Where(x => x.v.Manifiesto == null || !x.v.Manifiesto.StartsWith(prefijoVacio) || x.v.Manifiesto.Length != longitudVacio);

        if (filtro.SoloRecomendacionActiva)
            consulta = consulta.Where(x => db.AutorizacionesTanqueo.Any(a =>
                a.ViajeId == x.v.Id && (a.Estado == EstadoAutorizacion.Propuesta || a.Estado == EstadoAutorizacion.Autorizada)));

        var umbral = filtro.UmbralTelemetria;
        if (filtro.TelemetriaReciente == true)
            consulta = consulta.Where(x => x.ve.NivelAt >= umbral);
        else if (filtro.TelemetriaReciente == false)
            consulta = consulta.Where(x => x.ve.NivelAt == null || x.ve.NivelAt < umbral);

        // Rendimiento: se proyectan columnas (no entidades) y de la ruta solo los kilometros y los extremos ya
        // derivados por la base; la polilinea completa NO se lee en el listado.
        return await consulta
            .OrderByDescending(x => x.v.CreatedAt)
            .Select(x => new
            {
                x.v,
                x.ve,
                Ruta = db.Rutas
                    .Where(r => r.ViajeId == x.v.Id)
                    .OrderByDescending(r => r.FechaCalculo)
                    .Select(r => new { r.Kilometros, r.OrigenLat, r.OrigenLng, r.DestinoLat, r.DestinoLng })
                    .FirstOrDefault(),
                Recomendacion = db.AutorizacionesTanqueo.Any(a =>
                    a.ViajeId == x.v.Id && (a.Estado == EstadoAutorizacion.Propuesta || a.Estado == EstadoAutorizacion.Autorizada)),
            })
            .Select(y => new clsViajeListadoFila(
                y.v.Id,
                y.v.VehiculoId,
                y.v.Ruta,
                y.v.NombreRuta,
                y.v.CodigoRuta,
                y.v.Estado,
                y.v.AutonomiaEstimadaKm,
                y.v.Manifiesto,
                y.v.InicioAt,
                y.v.FechaDespacho,
                y.ve.Placa,
                y.ve.TipoMotor,
                y.ve.NivelTanqueActual,
                y.ve.CapacidadTanqueGal,
                y.ve.UbicacionAt,
                y.ve.NivelAt,
                y.ve.UltimaLat,
                y.ve.UltimaLng,
                y.ve.RendimientoHistorico,
                y.ve.RendimientoEsperado,
                y.ve.ToleranciaMotor,
                y.Ruta == null ? null : y.Ruta.Kilometros,
                y.Ruta == null ? null : y.Ruta.OrigenLat,
                y.Ruta == null ? null : y.Ruta.OrigenLng,
                y.Ruta == null ? null : y.Ruta.DestinoLat,
                y.Ruta == null ? null : y.Ruta.DestinoLng,
                y.Recomendacion))
            .ToListAsync(ct);
    }

    public async Task<clsViajesOpcionesDto> ObtenerOpcionesAsync(CancellationToken ct)
    {
        var activos = db.Viajes.AsNoTracking().Where(v => v.Estado != EstadoViaje.Cerrado);

        var rutas = await activos
            .Where(v => v.Ruta != "")
            .Select(v => v.Ruta)
            .Distinct()
            .OrderBy(r => r)
            .ToListAsync(ct);

        var anios = await activos
            .Where(v => v.FechaEfectiva != null)
            .Select(v => v.FechaEfectiva!.Value.Year)
            .Distinct()
            .OrderByDescending(a => a)
            .ToListAsync(ct);

        return new clsViajesOpcionesDto(rutas, anios);
    }

    public Task<clsViajeRutaFila?> ObtenerRutaAsync(Guid viajeId, CancellationToken ct) =>
        (from v in db.Viajes.AsNoTracking()
         join ve in db.Vehiculos.AsNoTracking() on v.VehiculoId equals ve.Id
         where v.Id == viajeId
         select new
         {
             v,
             ve,
             Ruta = db.Rutas
                 .Where(r => r.ViajeId == v.Id)
                 .OrderByDescending(r => r.FechaCalculo)
                 .Select(r => new { r.Kilometros, r.NumPuntos, r.Polilinea })
                 .FirstOrDefault(),
         })
        .Select(y => new clsViajeRutaFila(
            y.v.Id,
            y.v.Ruta,
            y.v.Estado,
            y.ve.Placa,
            y.ve.UltimaLat,
            y.ve.UltimaLng,
            y.ve.UbicacionAt,
            y.ve.NivelTanqueActual,
            y.ve.RendimientoHistorico,
            y.ve.RendimientoEsperado,
            y.ve.ToleranciaMotor,
            y.Ruta == null ? null : y.Ruta.Kilometros,
            y.Ruta == null ? null : y.Ruta.NumPuntos,
            y.Ruta == null ? null : y.Ruta.Polilinea))
        .FirstOrDefaultAsync(ct);
}
