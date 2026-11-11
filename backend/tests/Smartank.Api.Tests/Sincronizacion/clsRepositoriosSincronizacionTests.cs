using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Smartank.Api.Tests.Infra;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Domain.Enums;
using Smartank.Domain.Geo;
using Smartank.Infrastructure.Sincronizacion.Adaptadores;
using Smartank.Infrastructure.Sincronizacion.Repositorios;

namespace Smartank.Api.Tests.Sincronizacion;

/// <summary>
/// Ida y vuelta de las escrituras masivas (tabla temporal + SqlBulkCopy + UPDATE/INSERT por conjunto) contra el SQL Server
/// 2019 real con Database/ddl.sql aplicado. Se omiten sin TEST_DB_CONNECTION. Cada prueba usa llaves unicas propias (sufijo
/// aleatorio) y limpia lo que creo, asi que se puede correr sobre una base compartida.
/// </summary>
public class clsRepositoriosSincronizacionTests
{
    private static clsConexionSmartank Conexion() => new(new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:Smartank"] = clsHechoConBaseDePruebasAttribute.CadenaDeConexion })
        .Build());

    private static async Task<SqlConnection> Abrir()
    {
        var cn = new SqlConnection(clsHechoConBaseDePruebasAttribute.CadenaDeConexion);
        await cn.OpenAsync();
        return cn;
    }

    private static string Sufijo() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static clsCabezoteLegadoDto Cabezote(string placa, long fpsapId, decimal capacidad = 150m) =>
        new(fpsapId, placa, "DIESEL", capacidad, 10m, 140m, 2m, "CHIP", "900123", "ALIAS", true);

    [clsHechoConBaseDePruebas]
    public async Task Vehiculos_upsert_inserta_luego_actualiza_y_conserva_telemetria()
    {
        var s = Sufijo();
        var placa = "T" + s[..5];
        var fpsap = 9_000_000_000L + Random.Shared.Next(1, 1_000_000);
        var repo = new clsSqlVehiculosSincronizacionRepository(Conexion());
        await using var cn = await Abrir();
        try
        {
            var r1 = await repo.UpsertDesdeLegadoAsync([Cabezote(placa, fpsap)]);
            Assert.Equal((1, 0, 0), (r1.Insertados, r1.Actualizados, r1.Errores.Count));

            // Telemetria escrita por otro job: el upsert del maestro no debe borrarla.
            var id = await cn.ExecuteScalarAsync<Guid>("SELECT Id FROM dbo.tblVehiculos WHERE Placa = @placa", new { placa });
            var t = await repo.ActualizarTelemetriaAsync([new clsActualizacionTelemetriaDto(
                id, 4.711, -74.0721, DateTimeOffset.UtcNow, 93.75m, DateTimeOffset.UtcNow, 123.456, "bDEV" + s)]);
            Assert.Equal(1, t.Actualizados);

            var r2 = await repo.UpsertDesdeLegadoAsync([Cabezote(placa, fpsap, capacidad: 200m)]);
            Assert.Equal((0, 1), (r2.Insertados, r2.Actualizados));

            var fila = await cn.QuerySingleAsync("SELECT CapacidadTanqueGal, NivelTanqueActual, UltimaLat, Odometro, GeotabDeviceId, FpsapSincronizadoAt, UpdatedAt FROM dbo.tblVehiculos WHERE Id = @id", new { id });
            Assert.Equal(200m, (decimal)fila.CapacidadTanqueGal);
            Assert.Equal(93.75m, (decimal)fila.NivelTanqueActual);
            Assert.Equal(4.711m, (decimal)fila.UltimaLat);
            Assert.Equal(123.456m, (decimal)fila.Odometro);
            Assert.Equal("bDEV" + s, (string)fila.GeotabDeviceId);
            Assert.NotNull((object?)fila.FpsapSincronizadoAt);

            // Telemetria parcial: un campo nulo conserva el almacenado (COALESCE).
            await repo.ActualizarTelemetriaAsync([new clsActualizacionTelemetriaDto(id, 5.0, -75.0, null, null, null, null, "bDEV" + s)]);
            var tras = await cn.QuerySingleAsync("SELECT NivelTanqueActual, UltimaLat FROM dbo.tblVehiculos WHERE Id = @id", new { id });
            Assert.Equal(93.75m, (decimal)tras.NivelTanqueActual);
            Assert.Equal(5.0m, (decimal)tras.UltimaLat);

            // Rendimiento
            var rr = await repo.ActualizarRendimientoAsync([new clsRendimientoVehiculoDto(id, 8.33)]);
            Assert.Equal(1, rr.Actualizados);
            Assert.Equal(8.3300m, await cn.ExecuteScalarAsync<decimal>("SELECT RendimientoHistorico FROM dbo.tblVehiculos WHERE Id = @id", new { id }));

            var mapa = await repo.ObtenerMapaPlacaAIdAsync();
            Assert.Equal(id, mapa[placa.ToLowerInvariant()]);
        }
        finally
        {
            await cn.ExecuteAsync("DELETE FROM dbo.tblVehiculos WHERE Placa = @placa", new { placa });
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Viajes_upsert_es_idempotente_deriva_estado_y_aisla_la_fila_con_error()
    {
        var s = Sufijo();
        var placa = "V" + s[..5];
        var foBase = 8_000_000_000L + Random.Shared.Next(1, 1_000_000) * 10L;
        var vehiculos = new clsSqlVehiculosSincronizacionRepository(Conexion());
        var viajes = new clsSqlViajesSincronizacionRepository(Conexion());
        await using var cn = await Abrir();
        try
        {
            await vehiculos.UpsertDesdeLegadoAsync([Cabezote(placa, 9_100_000_000L + Random.Shared.Next(1, 1_000_000))]);
            var vehiculoId = (await vehiculos.ObtenerMapaPlacaAIdAsync())[placa];
            var despacho = new DateTimeOffset(2026, 3, 10, 13, 0, 0, TimeSpan.Zero);

            clsViajeUpsertDto Viaje(long fo, EstadoViaje estado = EstadoViaje.EnRuta, string ruta = "BOGOTA - CALI") => new(
                fo, vehiculoId, ruta, estado, "M" + fo, estado == EstadoViaje.Programado ? null : despacho, placa, "TR1", "PROG",
                "123", "TRANS", "N" + fo, "R1", ruta, "O", "D", 12345.5m, 1m, 2m, 3m, 4m, 5.5m, despacho,
                true, false, false, false, false);

            var lote = new[] { Viaje(foBase + 1), Viaje(foBase + 2, EstadoViaje.Programado), Viaje(foBase + 3, EstadoViaje.Cerrado) };
            var r1 = await viajes.UpsertAsync(lote);
            Assert.Equal((3, 0, 0), (r1.Insertados, r1.Actualizados, r1.Errores.Count));

            // Segunda corrida igual: ahora todo es actualizacion, sin duplicados.
            var r2 = await viajes.UpsertAsync(lote);
            Assert.Equal((0, 3, 0), (r2.Insertados, r2.Actualizados, r2.Errores.Count));
            Assert.Equal(3, await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.tblViajes WHERE FoId BETWEEN @a AND @b", new { a = foBase + 1, b = foBase + 3 }));

            var fila = await cn.QuerySingleAsync("SELECT Estado, Manifiesto, InicioAt, FechaDespacho, FechaEfectiva, PesoFo, KmtsRuta, GastosGenerados, Borrado, FoSincronizadoAt FROM dbo.tblViajes WHERE FoId = @fo", new { fo = foBase + 1 });
            Assert.Equal("en_ruta", (string)fila.Estado);
            Assert.Equal("M" + (foBase + 1), (string)fila.Manifiesto);
            Assert.Equal(despacho, (DateTimeOffset)fila.FechaDespacho);
            Assert.Equal(despacho, (DateTimeOffset)fila.FechaEfectiva);
            Assert.Equal(12345.5m, (decimal)fila.PesoFo);
            Assert.Equal(5.5m, (decimal)fila.KmtsRuta);
            Assert.True((bool)fila.GastosGenerados);
            Assert.NotNull((object?)fila.FoSincronizadoAt);
            Assert.Equal("programado", await cn.ExecuteScalarAsync<string>("SELECT Estado FROM dbo.tblViajes WHERE FoId = @fo", new { fo = foBase + 2 }));
            Assert.Null(await cn.ExecuteScalarAsync<DateTimeOffset?>("SELECT InicioAt FROM dbo.tblViajes WHERE FoId = @fo", new { fo = foBase + 2 }));

            // Un cambio en el legado se refleja: estado y ruta.
            await viajes.UpsertAsync([Viaje(foBase + 1, EstadoViaje.Cerrado, "OTRA RUTA")]);
            var cambiado = await cn.QuerySingleAsync("SELECT Estado, Ruta FROM dbo.tblViajes WHERE FoId = @fo", new { fo = foBase + 1 });
            Assert.Equal("cerrado", (string)cambiado.Estado);
            Assert.Equal("OTRA RUTA", (string)cambiado.Ruta);

            // Una fila con un dato invalido (Ruta es NOT NULL) se aisla: las demas del mismo lote se escriben.
            var mezcla = new[] { Viaje(foBase + 4), Viaje(foBase + 5) with { Ruta = null! }, Viaje(foBase + 6) };
            var r3 = await viajes.UpsertAsync(mezcla);
            Assert.Equal(2, r3.Insertados);
            var error = Assert.Single(r3.Errores);
            Assert.Contains((foBase + 5).ToString(), error.Clave);
            Assert.Equal(0, await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.tblViajes WHERE FoId = @fo", new { fo = foBase + 5 }));
            Assert.Equal(2, await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.tblViajes WHERE FoId IN (@a, @b)", new { a = foBase + 4, b = foBase + 6 }));

            // Listar para rutas devuelve los viajes con FO y su ruta aun inexistente.
            var paraRutas = await viajes.ListarParaRutasAsync();
            Assert.Contains(paraRutas, v => v.NumeroFo == "N" + (foBase + 1) && !v.TieneRuta);
        }
        finally
        {
            await cn.ExecuteAsync("DELETE FROM dbo.tblViajes WHERE FoId BETWEEN @a AND @b", new { a = foBase + 1, b = foBase + 6 });
            await cn.ExecuteAsync("DELETE FROM dbo.tblVehiculos WHERE Placa = @placa", new { placa });
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Tanqueos_upsert_por_fuente_y_id_acepta_placa_sin_vehiculo_y_aisla_el_error()
    {
        var s = Sufijo();
        var placa = "K" + s[..5];
        var idBase = 7_000_000_000L + Random.Shared.Next(1, 1_000_000) * 10L;
        var repo = new clsSqlTanqueosSincronizacionRepository(Conexion());
        await using var cn = await Abrir();
        try
        {
            clsTanqueoUpsertDto T(FuenteTanqueo f, long id, decimal galones = 50.123m) => new(
                new clsTanqueoLegadoDto(f, id, placa, new DateTimeOffset(2026, 3, 10, 18, 0, 0, TimeSpan.Zero), galones, 9_000.5m, 450_000m,
                    "ACPM", null, "Bogota", "EDS X", 123_456.789m, "123", "F" + id, "REF", "CC"), null);

            var lote = new[] { T(FuenteTanqueo.Distracom, idBase + 1), T(FuenteTanqueo.Terpel, idBase + 1), T(FuenteTanqueo.Distracom, idBase + 2) };
            var r1 = await repo.UpsertAsync(lote);
            Assert.Equal((3, 0, 0), (r1.Insertados, r1.Actualizados, r1.Errores.Count));

            // Misma corrida repetida con un galonaje distinto: se actualiza y no se duplica; Distracom 1 y Terpel 1 son filas distintas.
            var r2 = await repo.UpsertAsync([T(FuenteTanqueo.Distracom, idBase + 1, 60m), T(FuenteTanqueo.Terpel, idBase + 1), T(FuenteTanqueo.Distracom, idBase + 2)]);
            Assert.Equal((0, 3), (r2.Insertados, r2.Actualizados));
            Assert.Equal(3, await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.tblTanqueos WHERE Placa = @placa", new { placa }));

            var fila = await cn.QuerySingleAsync("SELECT Fuente, Galones, ValorGalon, Kilometraje, VehiculoId, Fecha, SincronizadoAt FROM dbo.tblTanqueos WHERE Fuente = 'distracom' AND FuenteId = @id", new { id = idBase + 1 });
            Assert.Equal(60m, (decimal)fila.Galones);
            Assert.Equal(9_000.5m, (decimal)fila.ValorGalon);
            Assert.Equal(123_456.789m, (decimal)fila.Kilometraje);
            Assert.Null((object?)fila.VehiculoId);
            Assert.Equal(new DateTimeOffset(2026, 3, 10, 18, 0, 0, TimeSpan.Zero), (DateTimeOffset)fila.Fecha);
            Assert.NotNull((object?)fila.SincronizadoAt);

            // CHECK Galones >= 0: la fila mala se aisla y la buena se inserta.
            var r3 = await repo.UpsertAsync([T(FuenteTanqueo.Distracom, idBase + 3, -5m), T(FuenteTanqueo.Distracom, idBase + 4)]);
            Assert.Equal(1, r3.Insertados);
            Assert.Contains((idBase + 3).ToString(), Assert.Single(r3.Errores).Clave);

            // Llaves repetidas dentro de la misma carga: gana la ultima.
            var r4 = await repo.UpsertAsync([T(FuenteTanqueo.Terpel, idBase + 5, 10m), T(FuenteTanqueo.Terpel, idBase + 5, 20m)]);
            Assert.Equal(1, r4.Insertados);
            Assert.Equal(20m, await cn.ExecuteScalarAsync<decimal>("SELECT Galones FROM dbo.tblTanqueos WHERE Fuente = 'terpel' AND FuenteId = @id", new { id = idBase + 5 }));
        }
        finally
        {
            await cn.ExecuteAsync("DELETE FROM dbo.tblTanqueos WHERE Placa = @placa", new { placa });
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Rutas_upsert_deriva_origen_y_destino_por_trigger_y_el_JSON_es_valido()
    {
        var fo = "TR" + Sufijo();
        var repo = new clsSqlRutasSincronizacionRepository(Conexion());
        await using var cn = await Abrir();
        try
        {
            var puntos = new[] { new clsCoordenada(4.711, -74.0721), new clsCoordenada(5.5, -74.5), new clsCoordenada(6.2442, -75.5812) };
            var polilinea = Smartank.Domain.Sincronizacion.Reglas.clsRutaPolilinea.AJson(puntos);

            clsRutaUpsertDto Ruta(string json, long valhalla) => new(
                fo, null, FuenteRuta.Valhalla, valhalla, 123.456, 3, 100, json, "{\"tramos\":[1,2]}", new DateTimeOffset(2026, 3, 1, 5, 0, 0, TimeSpan.Zero));

            var r1 = await repo.UpsertAsync([Ruta(polilinea, 11)]);
            Assert.Equal(1, r1.Insertados);
            var fila = await cn.QuerySingleAsync("SELECT Fuente, ValhallaId, Kilometros, NumPuntos, OrigenLat, OrigenLng, DestinoLat, DestinoLng, DetalleKm, SincronizadoAt FROM dbo.tblRutas WHERE Fo = @fo", new { fo });
            Assert.Equal("valhalla", (string)fila.Fuente);
            Assert.Equal(123.456m, (decimal)fila.Kilometros);
            Assert.Equal(4.711m, (decimal)fila.OrigenLat);
            Assert.Equal(-74.0721m, (decimal)fila.OrigenLng);
            Assert.Equal(6.2442m, (decimal)fila.DestinoLat);
            Assert.Equal(-75.5812m, (decimal)fila.DestinoLng);
            Assert.NotNull((object?)fila.SincronizadoAt);

            // Una polilinea nueva reescribe los extremos (el trigger corre tambien en el UPDATE).
            var nueva = Smartank.Domain.Sincronizacion.Reglas.clsRutaPolilinea.AJson([new clsCoordenada(1, 2), new clsCoordenada(3, 4)]);
            var r2 = await repo.UpsertAsync([Ruta(nueva, 12)]);
            Assert.Equal((0, 1), (r2.Insertados, r2.Actualizados));
            var despues = await cn.QuerySingleAsync("SELECT ValhallaId, OrigenLat, DestinoLng FROM dbo.tblRutas WHERE Fo = @fo", new { fo });
            Assert.Equal(12L, (long)despues.ValhallaId);
            Assert.Equal(1m, (decimal)despues.OrigenLat);
            Assert.Equal(4m, (decimal)despues.DestinoLng);
            Assert.Equal(1, await cn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.tblRutas WHERE Fo = @fo", new { fo }));

            // Un JSON invalido viola el CHECK ISJSON y queda aislado como error de esa fila.
            var r3 = await repo.UpsertAsync([Ruta("esto no es json", 13)]);
            Assert.Single(r3.Errores);
        }
        finally
        {
            await cn.ExecuteAsync("DELETE FROM dbo.tblRutas WHERE Fo = @fo", new { fo });
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Conductores_upsert_sobrescribe_el_celular_y_lo_deja_null_si_ya_no_hay_uno_valido()
    {
        var cedula = "C" + Sufijo();
        var repo = new clsSqlConductoresSincronizacionRepository(Conexion());
        await using var cn = await Abrir();
        try
        {
            var r1 = await repo.UpsertAsync([new clsConductorUpsertDto(900_000_001, cedula, "Juan", "+573046758799", true, false)]);
            Assert.Equal(1, r1.Insertados);
            var r2 = await repo.UpsertAsync([new clsConductorUpsertDto(900_000_001, cedula, "Juan P", null, false, true)]);
            Assert.Equal((0, 1), (r2.Insertados, r2.Actualizados));

            var fila = await cn.QuerySingleAsync("SELECT Nombres, NumeroCelular, Activo, UtilizarTarjetaPrepago FROM dbo.tblConductores WHERE Cedula = @cedula", new { cedula });
            Assert.Equal("Juan P", (string)fila.Nombres);
            Assert.Null((object?)fila.NumeroCelular);
            Assert.False((bool)fila.Activo);
            Assert.True((bool)fila.UtilizarTarjetaPrepago);
        }
        finally
        {
            await cn.ExecuteAsync("DELETE FROM dbo.tblConductores WHERE Cedula = @cedula", new { cedula });
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Estaciones_actualiza_precios_inserta_nuevas_y_calcula_la_referencia_por_viaje()
    {
        var s = Sufijo();
        var prov = "tprov" + s[..4];
        var repo = new clsSqlEstacionesSincronizacionRepository(Conexion());
        var vehiculos = new clsSqlVehiculosSincronizacionRepository(Conexion());
        var viajes = new clsSqlViajesSincronizacionRepository(Conexion());
        var rutas = new clsSqlRutasSincronizacionRepository(Conexion());
        var precios = new clsSqlPreciosRutaRepository(Conexion());
        var placa = "E" + s[..5];
        var fo = 6_000_000_000L + Random.Shared.Next(1, 1_000_000);
        var numeroFo = "ER" + s;
        await using var cn = await Abrir();
        try
        {
            var ins = await repo.InsertarAsync([
                new clsEstacionNuevaDto("EDS A " + s, 4.0, -73.5, prov, "X1", 10_000m, 12_000m, "ACPM - Corriente"),
                new clsEstacionNuevaDto("EDS B " + s, 4.01, -73.4, prov, null, null, 11_000m, "Corriente"),
            ]);
            Assert.Equal(2, ins.Insertados);

            var existentes = await repo.ListarPorProveedorAsync(prov);
            Assert.Equal(2, existentes.Count);
            var a = existentes.Single(e => e.CodigoExterno == "X1");
            var fila = await cn.QuerySingleAsync("SELECT PrecioGalonActual, SoportaPreautorizacion, PrecioActualizadoAt FROM dbo.tblEstaciones WHERE Id = @id", new { id = a.Id });
            Assert.Equal(10_000m, (decimal)fila.PrecioGalonActual);   // el ACPM
            Assert.True((bool)fila.SoportaPreautorizacion);
            Assert.NotNull((object?)fila.PrecioActualizadoAt);

            var b = existentes.Single(e => e.CodigoExterno is null);
            Assert.Equal(11_000m, await cn.ExecuteScalarAsync<decimal>("SELECT PrecioGalonActual FROM dbo.tblEstaciones WHERE Id = @id", new { id = b.Id }));

            // Actualizar: nuevo ACPM sobre A; B sin ACPM ni corriente conserva su precio actual.
            var up = await repo.ActualizarPreciosAsync([new clsActualizacionPrecioEstacionDto(a.Id, 9_500m, null), new clsActualizacionPrecioEstacionDto(b.Id, null, null)]);
            Assert.Equal(2, up.Actualizados);
            var tras = await cn.QuerySingleAsync("SELECT PrecioAcpm, PrecioCorriente, PrecioGalonActual FROM dbo.tblEstaciones WHERE Id = @id", new { id = a.Id });
            Assert.Equal(9_500m, (decimal)tras.PrecioAcpm);
            Assert.Null((object?)tras.PrecioCorriente);
            Assert.Equal(9_500m, (decimal)tras.PrecioGalonActual);
            Assert.Equal(11_000m, await cn.ExecuteScalarAsync<decimal>("SELECT PrecioGalonActual FROM dbo.tblEstaciones WHERE Id = @id", new { id = b.Id }));

            // Carga manual de un precio.
            var manual = await repo.ActualizarPrecioGalonAsync(a.Id, 9_123.456m);
            Assert.NotNull(manual);
            Assert.Equal(9_123.46m, manual!.PrecioGalonActual);
            Assert.Null(await repo.ActualizarPrecioGalonAsync(Guid.NewGuid(), 1m));
            var faltantes = await repo.ObtenerInexistentesAsync([a.Id, Guid.NewGuid()]);
            Assert.Single(faltantes);

            // Candidatas con ACPM: A si, B no (sin ACPM).
            var candidatas = await repo.ListarConPrecioAcpmAsync();
            Assert.Contains(candidatas, c => c.Lat == 4.0 && c.Lng == -73.5 && c.PrecioAcpm == 9_500m);

            // Precio de referencia por viaje: ruta sobre el paralelo 4 cerca de A.
            await vehiculos.UpsertDesdeLegadoAsync([Cabezote(placa, 9_200_000_000L + Random.Shared.Next(1, 1_000_000))]);
            var vehiculoId = (await vehiculos.ObtenerMapaPlacaAIdAsync())[placa];
            await viajes.UpsertAsync([new clsViajeUpsertDto(fo, vehiculoId, "R", EstadoViaje.EnRuta, null, null, placa, null, null, null, null,
                numeroFo, null, null, null, null, null, null, null, null, null, null, null, false, false, false, false, false)]);
            var viajeId = await cn.ExecuteScalarAsync<Guid>("SELECT Id FROM dbo.tblViajes WHERE FoId = @fo", new { fo });
            await rutas.UpsertAsync([new clsRutaUpsertDto(numeroFo, viajeId, FuenteRuta.Valhalla, 1, 111, 2, 2, "[[4,-74],[4,-73]]", null, null)]);

            var enFlujo = new List<clsRutaParaPrecioDto>();
            await foreach (var r in precios.ListarRutasConPolilineaAsync()) enFlujo.Add(r);
            var mia = enFlujo.Single(r => r.ViajeId == viajeId);
            Assert.Equal(2, mia.Polilinea.Count);

            var p1 = await precios.UpsertAsync([new clsPrecioReferenciaViajeDto(viajeId, 9_500m, 11_000m, 2)]);
            Assert.Equal(1, p1.Insertados);
            var p2 = await precios.UpsertAsync([new clsPrecioReferenciaViajeDto(viajeId, null, null, 0)]);
            Assert.Equal((0, 1), (p2.Insertados, p2.Actualizados));
            var refFila = await cn.QuerySingleAsync("SELECT AcpmMin, AcpmMax, EdsCerca FROM dbo.tblViajePreciosRef WHERE ViajeId = @viajeId", new { viajeId });
            Assert.Null((object?)refFila.AcpmMin);
            Assert.Equal(0, (int)refFila.EdsCerca);
        }
        finally
        {
            await cn.ExecuteAsync("DELETE FROM dbo.tblRutas WHERE Fo = @numeroFo", new { numeroFo });
            await cn.ExecuteAsync("DELETE FROM dbo.tblViajes WHERE FoId = @fo", new { fo });   // borra tblViajePreciosRef en cascada
            await cn.ExecuteAsync("DELETE FROM dbo.tblVehiculos WHERE Placa = @placa", new { placa });
            await cn.ExecuteAsync("DELETE FROM dbo.tblEstaciones WHERE Proveedor = @prov", new { prov });
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task Bloqueo_distribuido_es_exclusivo_entre_conexiones_y_se_libera()
    {
        var recurso = "smartank:test:" + Sufijo();
        var bloqueo = new clsBloqueoDistribuidoSqlServer(Conexion());

        var primero = await bloqueo.IntentarAdquirirAsync(recurso);
        Assert.NotNull(primero);

        // Otra "replica" (otra conexion) no lo obtiene mientras el primero lo tiene.
        var segundo = await bloqueo.IntentarAdquirirAsync(recurso);
        Assert.Null(segundo);

        // Otro recurso no se ve afectado.
        var otro = await bloqueo.IntentarAdquirirAsync(recurso + "-otro");
        Assert.NotNull(otro);
        await otro!.DisposeAsync();

        await primero!.DisposeAsync();
        var tercero = await bloqueo.IntentarAdquirirAsync(recurso);
        Assert.NotNull(tercero);
        await tercero!.DisposeAsync();
    }
}
