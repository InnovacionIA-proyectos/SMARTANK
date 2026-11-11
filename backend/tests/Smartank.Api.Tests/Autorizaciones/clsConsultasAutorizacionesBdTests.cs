using Microsoft.EntityFrameworkCore;
using Smartank.Api.Tests.Infra;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Domain.Tanqueos.Entidades;
using Smartank.Domain.Uploads.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;
using Smartank.Infrastructure.Autorizaciones.Repositorios;
using Smartank.Infrastructure.Bitacora.Repositorios;
using Smartank.Infrastructure.Eventos.Repositorios;
using Smartank.Infrastructure.Recomendacion.Repositorios;
using Smartank.Infrastructure.Uploads.Repositorios;

namespace Smartank.Api.Tests.Autorizaciones;

/// <summary>
/// Ejecuta contra SQL Server real las consultas de los repositorios del modulo (traduccion LINQ a SQL, joins, rowversion,
/// indices unicos). Se omiten sin TEST_DB_CONNECTION. Cada test usa datos propios (sufijo unico) y los borra al terminar.
/// </summary>
public class clsConsultasAutorizacionesBdTests
{
    private sealed class clsEscenario
    {
        public required clsVehiculo Vehiculo { get; init; }
        public required clsViaje Viaje { get; init; }
        public required clsEstacion Estacion { get; init; }
        public required clsAutorizacionTanqueo Autorizacion { get; init; }
    }

    private static async Task<clsEscenario> CrearAsync(Smartank.Infrastructure.Persistence.clsSmartankDbContext db, string sufijo)
    {
        var vehiculo = new clsVehiculo
        {
            Placa = ("T" + sufijo)[..6].ToUpperInvariant(),
            TipoMotor = "diesel",
            Programa = "TM_TEST",
            CapacidadTanqueGal = 300m,
            NivelTanqueActual = 100m,
            RendimientoHistorico = 5,
            ToleranciaMotor = 0.02,
            NivelAt = DateTimeOffset.UtcNow,
        };
        db.Vehiculos.Add(vehiculo);
        var estacion = new clsEstacion
        {
            Nombre = "EDS Prueba " + sufijo,
            Ruta = "RUTA-" + sufijo,
            Lat = 4.5,
            Lng = -74.1,
            PrecioAcpm = 9300m,
            PrecioGalonActual = 9300m,
            SoportaPreautorizacion = true,
        };
        db.Estaciones.Add(estacion);
        await db.SaveChangesAsync();

        var viaje = new clsViaje { VehiculoId = vehiculo.Id, Ruta = "Ruta " + sufijo, Estado = EstadoViaje.EnRuta, Manifiesto = "M" + sufijo };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        var autorizacion = new clsAutorizacionTanqueo
        {
            ViajeId = viaje.Id,
            VehiculoId = vehiculo.Id,
            EstacionId = estacion.Id,
            GalonesMax = 40m,
            PrecioTecho = 9300m,
            FechaInicio = DateTimeOffset.UtcNow.AddHours(-1),
            FechaFin = DateTimeOffset.UtcNow.AddHours(11),
        };
        db.AutorizacionesTanqueo.Add(autorizacion);
        await db.SaveChangesAsync();
        return new clsEscenario { Vehiculo = vehiculo, Viaje = viaje, Estacion = estacion, Autorizacion = autorizacion };
    }

    private static async Task LimpiarAsync(Smartank.Infrastructure.Persistence.clsSmartankDbContext db, clsEscenario e)
    {
        await db.EventosTanqueo.Where(x => x.AutorizacionId == e.Autorizacion.Id).ExecuteDeleteAsync();
        await db.AutorizacionesTanqueo.Where(x => x.ViajeId == e.Viaje.Id).ExecuteDeleteAsync();
        await db.Tanqueos.Where(x => x.Placa == e.Vehiculo.Placa).ExecuteDeleteAsync();
        await db.Viajes.Where(x => x.Id == e.Viaje.Id).ExecuteDeleteAsync();
        await db.Estaciones.Where(x => x.Id == e.Estacion.Id).ExecuteDeleteAsync();
        await db.Vehiculos.Where(x => x.Id == e.Vehiculo.Id).ExecuteDeleteAsync();
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8];

    [clsHechoConBaseDePruebas]
    public async Task Detalle_y_listado_de_autorizaciones_se_traducen_y_calculan_la_autonomia()
    {
        await using var db = clsContextoDePruebas.Crear();
        var e = await CrearAsync(db, Sufijo());
        try
        {
            var consultas = new clsEfAutorizacionesConsultas(db);

            var detalle = await consultas.ObtenerDetalleAsync(e.Autorizacion.Id, 8, default);
            var lista = await consultas.ListarDetallesAsync(new clsFiltroAutorizaciones(EstadoAutorizacion.Propuesta, e.Viaje.Id, 10), 8, default);

            Assert.NotNull(detalle);
            Assert.Equal(e.Vehiculo.Placa, detalle!.Placa);
            Assert.Equal(e.Estacion.Nombre, detalle.EstacionNombre);
            Assert.Equal("M" + e.Viaje.Manifiesto![1..], detalle.Manifiesto);
            Assert.Equal(490, detalle.AutonomiaKm); // 100 x 5 x 0.98
            Assert.Null(detalle.ConductorNombre);   // left join sin conductor
            Assert.Null(detalle.RutaKm);            // left join sin ruta
            Assert.Single(lista);
            Assert.Equal(e.Autorizacion.Id, lista[0].Id);
            Assert.Empty(await consultas.ListarDetallesAsync(new clsFiltroAutorizaciones(EstadoAutorizacion.Ejecutada, e.Viaje.Id, 10), 8, default));
            Assert.Null(await consultas.ObtenerDetalleAsync(Guid.NewGuid(), 8, default));
        }
        finally
        {
            await LimpiarAsync(db, e);
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Repositorios_de_autorizacion_propuesta_estaciones_y_polilinea_se_traducen()
    {
        await using var db = clsContextoDePruebas.Crear();
        var e = await CrearAsync(db, Sufijo());
        try
        {
            var autorizaciones = new clsEfAutorizacionesRepository(db);
            var recomendacion = new clsEfRecomendacionRepository(db);
            var eventos = new clsEfEventosRepository(db);

            var propuesta = await autorizaciones.ObtenerPropuestaDeViajeAsync(e.Viaje.Id, default);
            var estaciones = await recomendacion.ListarEstacionesConPrecioAcpmAsync(default);
            var polilinea = await recomendacion.ObtenerPolilineaDeViajeAsync(e.Viaje.Id, default);
            var minimo = await eventos.ObtenerPrecioMinimoDeRutaAsync(e.Estacion.Ruta!, default);

            Assert.Equal(e.Autorizacion.Id, propuesta!.Id);
            Assert.Contains(estaciones, x => x.Id == e.Estacion.Id);
            Assert.Null(polilinea);
            Assert.Equal(9300m, minimo);
            Assert.False(await eventos.ExistePorAutorizacionAsync(e.Autorizacion.Id, default));
        }
        finally
        {
            await LimpiarAsync(db, e);
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task La_base_impide_una_segunda_propuesta_del_viaje_y_Descartar_deja_el_contexto_limpio()
    {
        await using var db = clsContextoDePruebas.Crear();
        var e = await CrearAsync(db, Sufijo());
        try
        {
            var repo = new clsEfAutorizacionesRepository(db);
            var duplicada = new clsAutorizacionTanqueo
            {
                ViajeId = e.Viaje.Id, VehiculoId = e.Vehiculo.Id, EstacionId = e.Estacion.Id,
                GalonesMax = 10m, PrecioTecho = 1m,
                FechaInicio = DateTimeOffset.UtcNow, FechaFin = DateTimeOffset.UtcNow.AddHours(1),
            };
            repo.Agregar(duplicada);

            var ex = await Assert.ThrowsAsync<Smartank.Application.Common.Exceptions.clsConflictException>(
                () => new Smartank.Infrastructure.Common.clsEfUnitOfWork(db).SaveChangesAsync());
            repo.Descartar(duplicada);

            Assert.Equal("REGISTRO_DUPLICADO", ex.Code);
            Assert.Equal(EntityState.Detached, db.Entry(duplicada).State);
            // Con el contexto limpio un guardado posterior ya no reintenta el INSERT.
            Assert.Equal(0, await new Smartank.Infrastructure.Common.clsEfUnitOfWork(db).SaveChangesAsync());
        }
        finally
        {
            await LimpiarAsync(db, e);
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Autorizar_dos_veces_en_paralelo_solo_una_gana_por_el_rowversion()
    {
        await using var dbA = clsContextoDePruebas.Crear();
        await using var dbB = clsContextoDePruebas.Crear();
        var e = await CrearAsync(dbA, Sufijo());
        try
        {
            var a = await new clsEfAutorizacionesRepository(dbA).ObtenerAsync(e.Autorizacion.Id, default);
            var b = await new clsEfAutorizacionesRepository(dbB).ObtenerAsync(e.Autorizacion.Id, default);
            a!.Estado = EstadoAutorizacion.Autorizada;
            b!.Estado = EstadoAutorizacion.Rechazada;

            await new Smartank.Infrastructure.Common.clsEfUnitOfWork(dbA).SaveChangesAsync();
            var ex = await Assert.ThrowsAsync<Smartank.Application.Common.Exceptions.clsConflictException>(
                () => new Smartank.Infrastructure.Common.clsEfUnitOfWork(dbB).SaveChangesAsync());

            Assert.Equal("CONFLICTO_CONCURRENCIA", ex.Code);
        }
        finally
        {
            await LimpiarAsync(dbA, e);
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Bitacora_historial_y_uploads_se_traducen_y_el_evento_unico_se_hace_cumplir()
    {
        await using var db = clsContextoDePruebas.Crear();
        var e = await CrearAsync(db, Sufijo());
        var uploadId = Guid.Empty;
        try
        {
            // Evento de bitacora + tanqueo real casable (misma placa, estacion y fecha).
            db.EventosTanqueo.Add(new clsEventoTanqueo
            {
                AutorizacionId = e.Autorizacion.Id, VehiculoId = e.Vehiculo.Id, EstacionId = e.Estacion.Id,
                GalonesCargados = 40m, NivelAntes = 10m, NivelDespues = 49m, FotoSurtidorUrl = "https://x.test/f",
                OcurridoAt = DateTimeOffset.UtcNow,
            });
            db.Tanqueos.Add(new clsTanqueo
            {
                Fuente = FuenteTanqueo.Terpel, FuenteId = Random.Shared.NextInt64(1_000_000_000, long.MaxValue / 2),
                Placa = e.Vehiculo.Placa.ToLowerInvariant(), VehiculoId = e.Vehiculo.Id, Fecha = DateTimeOffset.UtcNow,
                Galones = 40m, ValorGalon = 9300m, Estacion = "  " + e.Estacion.Nombre.ToUpperInvariant() + " ", Ciudad = "Bogota",
            });
            await db.SaveChangesAsync();

            var bitacora = await new clsEfBitacoraConsultas(db).ObtenerAsync(e.Autorizacion.Id, default);
            var historial = await new clsEfEventosConsultas(db).ListarHistorialAsync(
                new clsFiltroHistorial(e.Vehiculo.Placa[1..5], FuenteTanqueo.Terpel, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1))), default);

            Assert.True(bitacora!.YaRegistrada);
            Assert.Equal(e.Estacion.Nombre, bitacora.EstacionNombre);
            Assert.Equal(EstadoAutorizacion.Propuesta, bitacora.Estado);
            var fila = Assert.Single(historial, h => h.Placa.Equals(e.Vehiculo.Placa, StringComparison.OrdinalIgnoreCase));
            Assert.True(fila.Autorizado);
            Assert.Equal(9300m, fila.PrecioTecho);
            Assert.Equal(49m, fila.NivelDespues);
            Assert.Equal("diesel", fila.TipoMotor);
            Assert.Equal("TM_TEST", fila.VehiculoPrograma);

            // Un segundo evento de la misma autorizacion lo rechaza el indice unico.
            var repoEventos = new clsEfEventosRepository(db);
            repoEventos.Agregar(new clsEventoTanqueo
            {
                AutorizacionId = e.Autorizacion.Id, VehiculoId = e.Vehiculo.Id, EstacionId = e.Estacion.Id, GalonesCargados = 1m,
            });
            var ex = await Assert.ThrowsAsync<Smartank.Application.Common.Exceptions.clsConflictException>(
                () => new Smartank.Infrastructure.Common.clsEfUnitOfWork(db).SaveChangesAsync());
            Assert.Equal("REGISTRO_DUPLICADO", ex.Code);

            // Uploads: ida y vuelta de los bytes.
            await using var db2 = clsContextoDePruebas.Crear();
            var uploads = new clsEfUploadsRepository(db2);
            var upload = new clsUpload { ContentType = "image/png", Data = [0x89, 0x50, 0x4E, 0x47, 1, 2, 3] };
            uploads.Agregar(upload);
            await new Smartank.Infrastructure.Common.clsEfUnitOfWork(db2).SaveChangesAsync();
            uploadId = upload.Id;
            var leido = await uploads.ObtenerAsync(uploadId, default);
            Assert.Equal("image/png", leido!.ContentType);
            Assert.Equal(upload.Data, leido.Data);
        }
        finally
        {
            await using var limpio = clsContextoDePruebas.Crear();
            if (uploadId != Guid.Empty) await limpio.Uploads.Where(u => u.Id == uploadId).ExecuteDeleteAsync();
            await LimpiarAsync(limpio, e);
        }
    }
}
