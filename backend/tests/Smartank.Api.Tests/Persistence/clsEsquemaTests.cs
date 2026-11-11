using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Smartank.Api.Tests.Infra;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Auditoria.Entidades;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Conductores.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Domain.Precios.Entidades;
using Smartank.Domain.Rutas.Entidades;
using Smartank.Domain.Tanqueos.Entidades;
using Smartank.Domain.Uploads.Entidades;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;
using Smartank.Infrastructure.Common;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Api.Tests.Persistence;

/// <summary>
/// Verifica que el mapeo de EF Core coincide con el esquema REAL de Database/ddl.sql. Necesita una base SQL Server 2019
/// con el DDL ya aplicado y la variable de entorno TEST_DB_CONNECTION; sin ella las pruebas se omiten.
/// </summary>
public class clsEsquemaTests
{
    [clsHechoConBaseDePruebas]
    public async Task CadaEntidad_SeConsultaSinError()
    {
        await using var db = clsContextoDePruebas.Crear();

        // Take(1) selecciona TODAS las columnas mapeadas: si falta o sobra una, SQL Server responde "Invalid column name".
        var consultas = new Dictionary<string, Func<Task>>
        {
            [nameof(db.Usuarios)] = () => db.Usuarios.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Vehiculos)] = () => db.Vehiculos.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Conductores)] = () => db.Conductores.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Estaciones)] = () => db.Estaciones.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Viajes)] = () => db.Viajes.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Rutas)] = () => db.Rutas.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.ViajePreciosRef)] = () => db.ViajePreciosRef.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Tanqueos)] = () => db.Tanqueos.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.AutorizacionesTanqueo)] = () => db.AutorizacionesTanqueo.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.EventosTanqueo)] = () => db.EventosTanqueo.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Alertas)] = () => db.Alertas.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.AuditLog)] = () => db.AuditLog.AsNoTracking().Take(1).ToListAsync(),
            [nameof(db.Uploads)] = () => db.Uploads.AsNoTracking().Take(1).ToListAsync(),
        };

        // Cada DbSet del contexto debe estar en la lista (si alguien agrega una entidad, esta prueba lo recuerda).
        var dbSets = typeof(clsSmartankDbContext).GetProperties()
            .Where(p => p.PropertyType.IsGenericType && p.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(p => p.Name);
        Assert.Empty(dbSets.Except(consultas.Keys));

        var fallos = new List<string>();
        foreach (var (nombre, consulta) in consultas)
        {
            try { await consulta(); }
            catch (Exception ex) { fallos.Add($"{nombre}: {ex.GetBaseException().Message}"); }
        }
        Assert.True(fallos.Count == 0, string.Join(Environment.NewLine, fallos));
    }

    [clsHechoConBaseDePruebas]
    public async Task IndicesYLlavesForaneasDelModelo_ExistenEnLaBase()
    {
        await using var db = clsContextoDePruebas.Crear();
        var modelo = db.GetService<IDesignTimeModel>().Model;
        var faltantes = new List<string>();

        foreach (var entidad in modelo.GetEntityTypes())
        {
            var tabla = entidad.GetTableName()!;
            var esquema = entidad.GetSchema() ?? "dbo";
            var objeto = $"[{esquema}].[{tabla}]";

            var indices = await db.Database
                .SqlQuery<string>($"SELECT [name] AS [Value] FROM sys.indexes WHERE object_id = OBJECT_ID({objeto}) AND [name] IS NOT NULL")
                .ToListAsync();
            var llaves = await db.Database
                .SqlQuery<string>($"SELECT [name] AS [Value] FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID({objeto})")
                .ToListAsync();
            var objetoId = await db.Database.SqlQuery<int?>($"SELECT OBJECT_ID({objeto}) AS [Value]").SingleAsync();
            if (objetoId is null) { faltantes.Add($"tabla {objeto}"); continue; }

            foreach (var indice in entidad.GetIndexes())
            {
                var nombre = indice.GetDatabaseName(StoreObjectIdentifier.Table(tabla, esquema));
                if (!indices.Contains(nombre!)) faltantes.Add($"indice {nombre} en {objeto}");
            }
            foreach (var fk in entidad.GetForeignKeys())
            {
                var principal = StoreObjectIdentifier.Table(fk.PrincipalEntityType.GetTableName()!, fk.PrincipalEntityType.GetSchema() ?? "dbo");
                var nombre = fk.GetConstraintName(StoreObjectIdentifier.Table(tabla, esquema), principal);
                if (!llaves.Contains(nombre!)) faltantes.Add($"FK {nombre} en {objeto}");
            }
        }

        Assert.True(faltantes.Count == 0, "El modelo de EF declara objetos que no existen en la base:" +
                                          Environment.NewLine + string.Join(Environment.NewLine, faltantes));
    }

    [clsHechoConBaseDePruebas]
    public async Task CadenaCompleta_InsertaLeeYBorra_TodasLasEntidades()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var ahora = DateTimeOffset.UtcNow;
        var ids = new Dictionary<string, Guid>();

        try
        {
            // --- Insert (maestros) -----------------------------------------------------------------------------
            await using (var db = clsContextoDePruebas.Crear())
            {
                // Enum: Rol <-> texto snake_case en la base.
                var usuario = new clsUsuario
                {
                    Email = $"prueba-{sufijo}@smartank.test", PasswordHash = "hash", Nombre = "Prueba", Rol = Rol.ControlFlota,
                };
                // double <-> decimal, bool false con default true en la base (no debe pisarse) y fecha.
                var vehiculo = new clsVehiculo
                {
                    Placa = $"T{sufijo}", TipoMotor = "diesel", Programa = "terpel", Activo = false,
                    CapacidadTanqueGal = 150.5m, RendimientoEsperado = 6.25, ToleranciaMotor = 0.05,
                    Odometro = 123456.789, UltimaLat = 6.1813655, UltimaLng = -75.591545, UbicacionAt = ahora,
                };
                var estacion = new clsEstacion
                {
                    Nombre = $"  Estacion PRUEBA {sufijo}  ", Lat = 6.2442, Lng = -75.5812, Proveedor = "terpel",
                    PrecioAcpm = 10049.5m, PrecioGalonActual = 10049.5m, SoportaPreautorizacion = true,
                };
                var conductor = new clsConductor { Cedula = $"C{sufijo}", Nombres = "Conductor Prueba", UtilizarTarjetaPrepago = true };
                db.AddRange(usuario, vehiculo, estacion, conductor);
                await db.SaveChangesAsync();

                ids["usuario"] = usuario.Id;
                ids["vehiculo"] = vehiculo.Id;
                ids["estacion"] = estacion.Id;
                ids["conductor"] = conductor.Id;
                Assert.NotEqual(Guid.Empty, usuario.Id);
                Assert.NotEqual(default, vehiculo.CreatedAt); // default de la base leido de vuelta
                // Columna calculada: minusculas sin espacios laterales.
                Assert.Equal($"estacion prueba {sufijo}", estacion.NombreNormalizado);
            }

            await using (var db = clsContextoDePruebas.Crear())
            {
                var usuario = await db.Usuarios.AsNoTracking().SingleAsync(x => x.Id == ids["usuario"]);
                Assert.Equal(Rol.ControlFlota, usuario.Rol);
                var rolCrudo = await db.Database
                    .SqlQuery<string>($"SELECT [Rol] AS [Value] FROM [dbo].[tblUsuarios] WHERE [Id] = {ids["usuario"]}")
                    .SingleAsync();
                Assert.Equal("control_flota", rolCrudo);

                var vehiculo = await db.Vehiculos.AsNoTracking().SingleAsync(x => x.Id == ids["vehiculo"]);
                Assert.False(vehiculo.Activo);
                Assert.Equal(0.05, vehiculo.ToleranciaMotor);
                Assert.Equal(6.25, vehiculo.RendimientoEsperado);
                Assert.Equal(6.1813655, vehiculo.UltimaLat!.Value, 7);
                Assert.Equal(150.5m, vehiculo.CapacidadTanqueGal);
                Assert.Equal(123456.789, vehiculo.Odometro!.Value, 3);

                var conductor = await db.Conductores.AsNoTracking().SingleAsync(x => x.Id == ids["conductor"]);
                Assert.True(conductor.UtilizarTarjetaPrepago);
                Assert.True(conductor.Activo);
            }

            // --- Insert (viaje, ruta con JSON, precios) ---------------------------------------------------------
            await using (var db = clsContextoDePruebas.Crear())
            {
                var viaje = new clsViaje
                {
                    VehiculoId = ids["vehiculo"], DespachadoPor = ids["usuario"], Ruta = "MEDELLIN-CARTAGENA",
                    Estado = EstadoViaje.EnRuta, Manifiesto = $"M{sufijo}", FoId = Random.Shared.NextInt64(1, long.MaxValue),
                    NumeroFo = "0000123", FechaDespacho = ahora.AddHours(-3), GalonesEds = 20.5m, KmtsRuta = 640.25, Borrado = false,
                };
                db.Viajes.Add(viaje);
                await db.SaveChangesAsync();
                ids["viaje"] = viaje.Id;
                // Columna calculada isnull(InicioAt, FechaDespacho).
                Assert.Equal(viaje.FechaDespacho, viaje.FechaEfectiva);

                var polilinea = new[] { new[] { 6.2442, -75.5812 }, new[] { 8.0, -75.0 }, new[] { 10.391, -75.4794 } };
                var ruta = new clsRuta
                {
                    Fo = $"123{sufijo}", ViajeId = viaje.Id, Kilometros = 640.123, NumPuntos = 3, NumPuntosOriginal = 7700,
                    Polilinea = polilinea, DetalleKm = "[{\"cost\":1.5,\"time\":10}]", FechaCalculo = ahora,
                };
                db.Add(ruta);
                db.Add(new clsViajePrecioRef { ViajeId = viaje.Id, AcpmMin = 9800m, AcpmMax = 11200.5m, EdsCerca = 4 });
                await db.SaveChangesAsync();
                ids["ruta"] = ruta.Id;

                // El trigger TRG_tblRutas_Derivados llena origen y destino a partir de la polilinea.
                Assert.Equal(6.2442, ruta.OrigenLat!.Value, 7);
                Assert.Equal(10.391, ruta.DestinoLat!.Value, 7);
                Assert.Equal(-75.4794, ruta.DestinoLng!.Value, 7);
            }

            await using (var db = clsContextoDePruebas.Crear())
            {
                var ruta = await db.Rutas.AsNoTracking().SingleAsync(x => x.Id == ids["ruta"]);
                Assert.Equal(FuenteRuta.Valhalla, ruta.Fuente); // default de la base / de la entidad
                Assert.Equal(3, ruta.Polilinea!.Length);
                Assert.Equal(-75.5812, ruta.Polilinea[0][1]);
                Assert.Contains("cost", ruta.DetalleKm);
                Assert.Equal(10.391, ruta.DestinoLat!.Value, 7);
                Assert.Equal(640.123, ruta.Kilometros!.Value, 3);

                var precios = await db.ViajePreciosRef.AsNoTracking().SingleAsync(x => x.ViajeId == ids["viaje"]);
                Assert.Equal(11200.5m, precios.AcpmMax);
                Assert.NotEqual(default, precios.CalculadoAt);
            }

            // --- Autorizacion: enum + JSON + rowversion + trigger de UpdatedAt --------------------------------------
            byte[] versionInicial;
            await using (var db = clsContextoDePruebas.Crear())
            {
                var autorizacion = new clsAutorizacionTanqueo
                {
                    ViajeId = ids["viaje"], VehiculoId = ids["vehiculo"], EstacionId = ids["estacion"], GalonesMax = 100.5m,
                    PrecioTecho = 10500m, DeltaAhorroEstimado = 250000.5m, FechaInicio = ahora, FechaFin = ahora.AddHours(12),
                    EdsConfirmacion = "{\"simulado\":true}",
                };
                db.AutorizacionesTanqueo.Add(autorizacion);
                await db.SaveChangesAsync();
                ids["autorizacion"] = autorizacion.Id;
                Assert.Equal(8, autorizacion.RowVersion.Length);
                versionInicial = autorizacion.RowVersion;
            }

            // Una segunda propuesta para el mismo viaje la rechaza el indice unico filtrado.
            await using (var db = clsContextoDePruebas.Crear())
            {
                db.AutorizacionesTanqueo.Add(new clsAutorizacionTanqueo
                {
                    ViajeId = ids["viaje"], VehiculoId = ids["vehiculo"], EstacionId = ids["estacion"], GalonesMax = 10m,
                    PrecioTecho = 1m, FechaInicio = ahora, FechaFin = ahora.AddHours(1),
                });
                var ex = await Assert.ThrowsAsync<clsConflictException>(() => new clsEfUnitOfWork(db).SaveChangesAsync());
                Assert.Equal("REGISTRO_DUPLICADO", ex.Code);
            }

            await using (var db = clsContextoDePruebas.Crear())
            await using (var otraSesion = clsContextoDePruebas.Crear())
            {
                var autorizacion = await db.AutorizacionesTanqueo.SingleAsync(x => x.Id == ids["autorizacion"]);
                var copiaStale = await otraSesion.AutorizacionesTanqueo.SingleAsync(x => x.Id == ids["autorizacion"]);
                Assert.Equal(EstadoAutorizacion.Propuesta, autorizacion.Estado);
                Assert.Equal("{\"simulado\":true}", autorizacion.EdsConfirmacion);

                autorizacion.Estado = EstadoAutorizacion.Autorizada;
                await new clsEfUnitOfWork(db).SaveChangesAsync();
                Assert.NotEqual(versionInicial, autorizacion.RowVersion);          // rowversion releido tras el UPDATE
                Assert.True(autorizacion.UpdatedAt >= autorizacion.CreatedAt);      // trigger de UpdatedAt

                // La otra sesion tiene el rowversion viejo: el UPDATE no afecta filas -> conflicto.
                copiaStale.Estado = EstadoAutorizacion.Rechazada;
                var ex = await Assert.ThrowsAsync<clsConflictException>(() => new clsEfUnitOfWork(otraSesion).SaveChangesAsync());
                Assert.Equal("CONFLICTO_CONCURRENCIA", ex.Code);
            }

            // --- Evento (uno por autorizacion), alerta (enum + rowversion), tanqueo, upload, auditoria ------------------
            await using (var db = clsContextoDePruebas.Crear())
            {
                var evento = new clsEventoTanqueo
                {
                    AutorizacionId = ids["autorizacion"], VehiculoId = ids["vehiculo"], EstacionId = ids["estacion"],
                    GalonesCargados = 95.25m, PrecioPagado = 10400m, OdometroEvento = 123500.5, NivelAntes = 10m, NivelDespues = 105.25m,
                    Programa = "terpel", Producto = "diesel", RegistradoPor = ids["usuario"],
                };
                db.EventosTanqueo.Add(evento);
                await db.SaveChangesAsync();
                ids["evento"] = evento.Id;
                Assert.NotEqual(default, evento.OcurridoAt); // default de la base

                var alerta = new clsAlerta
                {
                    Tipo = TipoAlerta.DesviacionConsumoCarga, VehiculoId = ids["vehiculo"], EventoId = evento.Id, MagnitudDesviacion = 0.1234,
                };
                var tanqueo = new clsTanqueo
                {
                    Fuente = FuenteTanqueo.Terpel, FuenteId = Random.Shared.NextInt64(1, long.MaxValue), VehiculoId = ids["vehiculo"],
                    Placa = $"T{sufijo}", Fecha = ahora, Galones = 50.25m, ValorGalon = 10300m, ValorTotal = 517575m, Kilometraje = 123400.5,
                };
                var upload = new clsUpload { ContentType = "image/jpeg", Data = [0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02] };
                db.AddRange(alerta, tanqueo, upload);
                await db.SaveChangesAsync();
                ids["alerta"] = alerta.Id;
                ids["tanqueo"] = tanqueo.Id;
                ids["upload"] = upload.Id;
                Assert.Equal(8, alerta.RowVersion.Length);
            }

            await using (var db = clsContextoDePruebas.Crear())
            {
                // Un segundo evento para la misma autorizacion viola UX_tblEventosTanqueo_AutorizacionId.
                db.EventosTanqueo.Add(new clsEventoTanqueo
                {
                    AutorizacionId = ids["autorizacion"], VehiculoId = ids["vehiculo"], EstacionId = ids["estacion"], GalonesCargados = 1m,
                });
                var ex = await Assert.ThrowsAsync<clsConflictException>(() => new clsEfUnitOfWork(db).SaveChangesAsync());
                Assert.Equal("REGISTRO_DUPLICADO", ex.Code);
            }

            await using (var db = clsContextoDePruebas.Crear())
            {
                var alerta = await db.Alertas.AsNoTracking().SingleAsync(x => x.Id == ids["alerta"]);
                Assert.Equal(TipoAlerta.DesviacionConsumoCarga, alerta.Tipo);
                Assert.Equal(EstadoAlerta.Nueva, alerta.Estado);
                Assert.Equal(0.1234, alerta.MagnitudDesviacion!.Value, 4);

                var tanqueo = await db.Tanqueos.AsNoTracking().SingleAsync(x => x.Id == ids["tanqueo"]);
                Assert.Equal(FuenteTanqueo.Terpel, tanqueo.Fuente);
                Assert.Equal(517575m, tanqueo.ValorTotal);

                var upload = await db.Uploads.AsNoTracking().SingleAsync(x => x.Id == ids["upload"]);
                Assert.Equal([0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02], upload.Data);

                var evento = await db.EventosTanqueo.AsNoTracking().SingleAsync(x => x.Id == ids["evento"]);
                Assert.Equal(95.25m, evento.GalonesCargados);
                Assert.Equal(123500.5, evento.OdometroEvento!.Value, 3);
            }

            // Auditoria: diff objeto, diff escalar (se envuelve para cumplir ISJSON) y diff nulo.
            await using (var db = clsContextoDePruebas.Crear())
            {
                var auditoria = new clsEfAuditoriaRepository(db);
                await auditoria.RegistrarAsync(null, "autorizar_tanqueo", "autorizacion", ids["autorizacion"],
                    new { estado_anterior = EstadoAutorizacion.Propuesta, estado = EstadoAutorizacion.Autorizada });
                await auditoria.RegistrarAsync(null, "prueba_escalar", "autorizacion", ids["autorizacion"], "solo texto");
                await auditoria.RegistrarAsync(null, "prueba_nulo", "autorizacion", ids["autorizacion"]);
            }

            await using (var db = clsContextoDePruebas.Crear())
            {
                var registros = await db.AuditLog.AsNoTracking()
                    .Where(x => x.EntidadId == ids["autorizacion"]).OrderBy(x => x.Accion).ToListAsync();
                Assert.Equal(3, registros.Count);
                Assert.Contains("\"estado_anterior\":\"propuesta\"", registros.Single(x => x.Accion == "autorizar_tanqueo").Diff);
                Assert.Equal("{\"valor\":\"solo texto\"}", registros.Single(x => x.Accion == "prueba_escalar").Diff);
                Assert.Null(registros.Single(x => x.Accion == "prueba_nulo").Diff);
            }
        }
        finally
        {
            await LimpiarAsync(ids);
        }
    }

    [clsHechoConBaseDePruebas]
    public async Task ConsultasConFiltros_SobreConversionesYEnums_Traducen()
    {
        await using var db = clsContextoDePruebas.Crear();

        // double sobre decimal(10,7), enum sobre texto snake_case, columna calculada y filtro del indice filtrado.
        var cercanas = await db.Estaciones.AsNoTracking()
            .Where(e => e.Lat >= 6.0 && e.Lat <= 6.5 && e.Lng >= -76.0 && e.Lng <= -75.0)
            .OrderBy(e => e.Lat).Take(5).ToListAsync();
        Assert.All(cercanas, e => Assert.InRange(e.Lat!.Value, 6.0, 6.5));

        var porNombre = await db.Estaciones.AsNoTracking()
            .Where(e => e.NombreNormalizado == "eds yumbo").Select(e => e.CodigoExterno).ToListAsync();
        Assert.NotEmpty(porNombre);

        var enRuta = await db.Viajes.AsNoTracking()
            .Where(v => v.Estado == EstadoViaje.EnRuta && v.FoId != null).Take(1).ToListAsync();
        var propuestas = await db.AutorizacionesTanqueo.AsNoTracking()
            .Where(a => a.Estado == EstadoAutorizacion.Propuesta).Take(1).ToListAsync();
        var sinPolilinea = await db.Rutas.AsNoTracking().Select(r => new { r.Fo, r.Kilometros }).Take(1).ToListAsync();
        Assert.NotNull(enRuta);
        Assert.NotNull(propuestas);
        Assert.NotNull(sinPolilinea);
    }

    [clsHechoConBaseDePruebas]
    public async Task AuditLog_EsAppendOnly_BloqueaUpdateYDelete()
    {
        await using var db = clsContextoDePruebas.Crear();
        var accion = $"prueba_append_only_{Guid.NewGuid():N}";
        await new clsEfAuditoriaRepository(db).RegistrarAsync(null, accion, "prueba", null, new { ok = true });

        var update = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"UPDATE [dbo].[tblAuditLog] SET [Entidad] = N'otra' WHERE [Accion] = {accion}"));
        Assert.Equal(50001, update.Number);
        Assert.Contains("append-only", update.Message);

        var delete = await Assert.ThrowsAsync<SqlException>(() =>
            db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM [dbo].[tblAuditLog] WHERE [Accion] = {accion}"));
        Assert.Equal(50001, delete.Number);

        // El registro sigue intacto.
        var registro = await db.AuditLog.AsNoTracking().SingleAsync(x => x.Accion == accion);
        Assert.Equal("prueba", registro.Entidad);
        Assert.Equal(registro.CreatedAt, registro.UpdatedAt);
    }

    [clsHechoConBaseDePruebas]
    public async Task ChecksDeEnumsYJson_RechazanValoresInvalidos()
    {
        await using var db = clsContextoDePruebas.Crear();
        var sufijo = Guid.NewGuid().ToString("N")[..8];

        // Rol fuera del CHECK.
        var rol = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [dbo].[tblUsuarios] ([Email], [PasswordHash], [Rol]) VALUES ({sufijo + "@x.test"}, N'h', N'superadmin')"));
        Assert.Contains("CK_tblUsuarios_Rol", rol.Message);

        // JSON invalido en el diff de auditoria.
        var json = await Assert.ThrowsAsync<SqlException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [dbo].[tblAuditLog] ([Accion], [Entidad], [Diff]) VALUES (N'a', N'b', N'no es json')"));
        Assert.Contains("CK_tblAuditLog_Diff", json.Message);
    }

    /// <summary>Borra lo creado por la prueba en orden inverso de dependencias (tblAuditLog no se puede borrar).</summary>
    private static async Task LimpiarAsync(Dictionary<string, Guid> ids)
    {
        await using var db = clsContextoDePruebas.Crear();
        // Las lambdas de EF no admiten funciones locales: se sacan los ids a variables.
        var alerta = ids.GetValueOrDefault("alerta");
        var evento = ids.GetValueOrDefault("evento");
        var viaje = ids.GetValueOrDefault("viaje");
        var tanqueo = ids.GetValueOrDefault("tanqueo");
        var ruta = ids.GetValueOrDefault("ruta");
        var estacion = ids.GetValueOrDefault("estacion");
        var conductor = ids.GetValueOrDefault("conductor");
        var vehiculo = ids.GetValueOrDefault("vehiculo");
        var usuario = ids.GetValueOrDefault("usuario");
        var upload = ids.GetValueOrDefault("upload");

        await db.Alertas.Where(x => x.Id == alerta).ExecuteDeleteAsync();
        await db.EventosTanqueo.Where(x => x.Id == evento).ExecuteDeleteAsync();
        await db.AutorizacionesTanqueo.Where(x => x.ViajeId == viaje).ExecuteDeleteAsync();
        await db.Tanqueos.Where(x => x.Id == tanqueo).ExecuteDeleteAsync();
        await db.Rutas.Where(x => x.Id == ruta).ExecuteDeleteAsync();
        await db.Viajes.Where(x => x.Id == viaje).ExecuteDeleteAsync(); // tblViajePreciosRef se va en cascada
        await db.Estaciones.Where(x => x.Id == estacion).ExecuteDeleteAsync();
        await db.Conductores.Where(x => x.Id == conductor).ExecuteDeleteAsync();
        await db.Vehiculos.Where(x => x.Id == vehiculo).ExecuteDeleteAsync();
        await db.Usuarios.Where(x => x.Id == usuario).ExecuteDeleteAsync();
        await db.Uploads.Where(x => x.Id == upload).ExecuteDeleteAsync();
    }
}
