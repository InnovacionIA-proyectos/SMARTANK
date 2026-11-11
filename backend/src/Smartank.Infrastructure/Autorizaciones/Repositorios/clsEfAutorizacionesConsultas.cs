using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Enums;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Autorizaciones.Repositorios;

/// <summary>
/// Lecturas de autorizaciones con sus joins (estacion, vehiculo, viaje, conductor y ruta) para las pantallas. Se proyectan solo
/// las columnas necesarias (nunca la polilinea) y la autonomia se calcula con la regla del dominio.
/// </summary>
public sealed class clsEfAutorizacionesConsultas(clsSmartankDbContext db) : IAutorizacionesConsultas
{
    public async Task<clsAutorizacionDetalleDto?> ObtenerDetalleAsync(Guid id, double rendimientoDefaultKmGal, CancellationToken ct)
    {
        var filas = await Proyectar(db.AutorizacionesTanqueo.AsNoTracking().Where(a => a.Id == id)).Take(1).ToListAsync(ct);
        return filas.Select(f => ADto(f, rendimientoDefaultKmGal)).FirstOrDefault();
    }

    public async Task<IReadOnlyList<clsAutorizacionDetalleDto>> ListarDetallesAsync(
        clsFiltroAutorizaciones filtro, double rendimientoDefaultKmGal, CancellationToken ct)
    {
        var origen = db.AutorizacionesTanqueo.AsNoTracking().AsQueryable();
        if (filtro.Estado is { } estado) origen = origen.Where(a => a.Estado == estado);
        if (filtro.ViajeId is { } viajeId) origen = origen.Where(a => a.ViajeId == viajeId);

        var filas = await Proyectar(origen).OrderByDescending(f => f.CreatedAt).Take(filtro.Limite).ToListAsync(ct);
        return filas.Select(f => ADto(f, rendimientoDefaultKmGal)).ToList();
    }

    /// <summary>Une la autorizacion con estacion, vehiculo y viaje (obligatorios por FK) y con conductor y ruta (opcionales).</summary>
    private IQueryable<clsFilaDetalle> Proyectar(IQueryable<clsAutorizacionTanqueo> origen) =>
        from a in origen
        join e in db.Estaciones on a.EstacionId equals e.Id
        join ve in db.Vehiculos on a.VehiculoId equals ve.Id
        join v in db.Viajes on a.ViajeId equals v.Id
        join co in db.Conductores on v.CedulaConductor equals co.Cedula into conductores
        from co in conductores.DefaultIfEmpty()
        join r in db.Rutas on v.Id equals r.ViajeId into rutas
        from r in rutas.DefaultIfEmpty()
        select new clsFilaDetalle
        {
            Id = a.Id,
            ViajeId = a.ViajeId,
            Placa = ve.Placa,
            Manifiesto = v.Manifiesto,
            ViajeRuta = v.Ruta,
            ViajeNombreRuta = v.NombreRuta,
            ViajeCodigoRuta = v.CodigoRuta,
            Programa = v.ProgramaCabezote,
            CedulaConductor = v.CedulaConductor,
            ConductorNombre = co.Nombres,
            EstacionId = a.EstacionId,
            EstacionNombre = e.Nombre,
            EstacionRuta = e.Ruta,
            EstacionLat = e.Lat,
            EstacionLng = e.Lng,
            VehiculoLat = ve.UltimaLat,
            VehiculoLng = ve.UltimaLng,
            VehiculoUbicacionAt = ve.UbicacionAt,
            VehiculoNivelAt = ve.NivelAt,
            NivelTanqueActual = ve.NivelTanqueActual,
            CapacidadTanqueGal = ve.CapacidadTanqueGal,
            RendimientoHistorico = ve.RendimientoHistorico,
            RendimientoEsperado = ve.RendimientoEsperado,
            ToleranciaMotor = ve.ToleranciaMotor,
            RutaKm = r.Kilometros,
            OrigenLat = r.OrigenLat,
            OrigenLng = r.OrigenLng,
            DestinoLat = r.DestinoLat,
            DestinoLng = r.DestinoLng,
            GalonesMax = a.GalonesMax,
            PrecioTecho = a.PrecioTecho,
            DeltaAhorroEstimado = a.DeltaAhorroEstimado,
            Estado = a.Estado,
            FechaInicio = a.FechaInicio,
            FechaFin = a.FechaFin,
            CreatedAt = a.CreatedAt,
        };

    private static clsAutorizacionDetalleDto ADto(clsFilaDetalle f, double rendimientoDefault)
    {
        // Autonomia actual del vehiculo en km enteros (el origen la redondeaba en SQL): nivel x rendimiento x (1 - tolerancia).
        double? autonomia = null;
        if (f.NivelTanqueActual is { } nivel)
        {
            var rendimiento = clsAutonomia.ResolverRendimiento(f.RendimientoHistorico, f.RendimientoEsperado, rendimientoDefault);
            autonomia = clsAutonomia.Redondear(clsAutonomia.CalcularKm((double)nivel, rendimiento.KmPorGalon, f.ToleranciaMotor), 0);
        }

        return new clsAutorizacionDetalleDto
        {
            Id = f.Id,
            ViajeId = f.ViajeId,
            Placa = f.Placa,
            Manifiesto = f.Manifiesto,
            ViajeRuta = f.ViajeRuta,
            ViajeNombreRuta = f.ViajeNombreRuta,
            ViajeCodigoRuta = f.ViajeCodigoRuta,
            Programa = f.Programa,
            CedulaConductor = f.CedulaConductor,
            ConductorNombre = f.ConductorNombre,
            EstacionId = f.EstacionId,
            EstacionNombre = f.EstacionNombre,
            EstacionRuta = f.EstacionRuta,
            EstacionLat = f.EstacionLat,
            EstacionLng = f.EstacionLng,
            VehiculoLat = f.VehiculoLat,
            VehiculoLng = f.VehiculoLng,
            VehiculoUbicacionAt = f.VehiculoUbicacionAt,
            VehiculoNivelAt = f.VehiculoNivelAt,
            NivelTanqueActual = f.NivelTanqueActual,
            CapacidadTanqueGal = f.CapacidadTanqueGal,
            AutonomiaKm = autonomia,
            RutaKm = f.RutaKm,
            OrigenLat = f.OrigenLat,
            OrigenLng = f.OrigenLng,
            DestinoLat = f.DestinoLat,
            DestinoLng = f.DestinoLng,
            GalonesMax = f.GalonesMax,
            PrecioTecho = f.PrecioTecho,
            DeltaAhorroEstimado = f.DeltaAhorroEstimado,
            Estado = f.Estado,
            FechaInicio = f.FechaInicio,
            FechaFin = f.FechaFin,
        };
    }

    /// <summary>Fila intermedia de la consulta: los datos crudos que el caso de lectura combina (la autonomia sale de ellos).</summary>
    private sealed class clsFilaDetalle
    {
        public Guid Id { get; init; }
        public Guid ViajeId { get; init; }
        public string Placa { get; init; } = string.Empty;
        public string? Manifiesto { get; init; }
        public string? ViajeRuta { get; init; }
        public string? ViajeNombreRuta { get; init; }
        public string? ViajeCodigoRuta { get; init; }
        public string? Programa { get; init; }
        public string? CedulaConductor { get; init; }
        public string? ConductorNombre { get; init; }
        public Guid EstacionId { get; init; }
        public string EstacionNombre { get; init; } = string.Empty;
        public string? EstacionRuta { get; init; }
        public double? EstacionLat { get; init; }
        public double? EstacionLng { get; init; }
        public double? VehiculoLat { get; init; }
        public double? VehiculoLng { get; init; }
        public DateTimeOffset? VehiculoUbicacionAt { get; init; }
        public DateTimeOffset? VehiculoNivelAt { get; init; }
        public decimal? NivelTanqueActual { get; init; }
        public decimal? CapacidadTanqueGal { get; init; }
        public double? RendimientoHistorico { get; init; }
        public double? RendimientoEsperado { get; init; }
        public double ToleranciaMotor { get; init; }
        public double? RutaKm { get; init; }
        public double? OrigenLat { get; init; }
        public double? OrigenLng { get; init; }
        public double? DestinoLat { get; init; }
        public double? DestinoLng { get; init; }
        public decimal GalonesMax { get; init; }
        public decimal PrecioTecho { get; init; }
        public decimal? DeltaAhorroEstimado { get; init; }
        public EstadoAutorizacion Estado { get; init; }
        public DateTimeOffset FechaInicio { get; init; }
        public DateTimeOffset FechaFin { get; init; }
        public DateTimeOffset CreatedAt { get; init; }
    }
}
