using Smartank.Api.Tests.Infra;
using Smartank.Application.CasosDeUso.Metricas;
using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Domain.Enums;
using Smartank.Infrastructure.Metricas.Repositorios;

namespace Smartank.Api.Tests.Metricas;

/// <summary>
/// Consulta de tanqueos del tablero (SQL con OUTER APPLY) y opciones de filtro contra SQL Server; se omite sin
/// TEST_DB_CONNECTION. Los totales se verifican a traves del handler real, con datos calculados a mano.
/// </summary>
public class clsMetricasRepositoryTests
{
    private static readonly double[][] POLILINEA = [[4.6, -74.1], [4.4, -73.9], [4.15, -73.6]];
    private static readonly TimeSpan BOGOTA = TimeSpan.FromHours(-5);

    private static clsRecuperacionFiltrosDto Sin() => new(null, null, null, null, null, null);

    private sealed record Datos(clsEscenarioBd E, string Placa, string Ruta, Guid ViajeId, Guid EdsId);

    /// <summary>
    /// Vehiculo con un viaje iniciado el 1-jun-2026 (ruta con polilinea; referencia ACPM min 9.000 / max 10.000) y dos tanqueos:
    /// 10-jun terpel 40 gal a 9.300 en "EDS {marca}" (casa con el catalogo) y 20-jun distracom 50 gal a 9.500 en
    /// "{marca} SUR" (casa con "DISTRACOM {marca} SUR" del catalogo). Un tercer tanqueo, ANTES del viaje, no se liga a el.
    /// </summary>
    private static async Task<Datos> SembrarAsync(clsEscenarioBd e)
    {
        var placa = e.Placa(1);
        var ruta = $"Ruta {e.Marca}";
        var ve = await e.AgregarVehiculoAsync(placa, v => v.Programa = $"PROG-{e.Marca}");
        var viaje = await e.AgregarViajeAsync(ve.Id, ruta, v =>
        {
            v.NombreRuta = ruta;
            v.InicioAt = new DateTimeOffset(2026, 6, 1, 6, 0, 0, BOGOTA);
        });
        await e.AgregarRutaAsync(viaje.Id, POLILINEA, 666);
        await e.AgregarPrecioRefAsync(viaje.Id, 9000m, 10000m);
        var eds = await e.AgregarEstacionAsync($"EDS {e.Marca}", x =>
        {
            x.Lat = 4.65;
            x.Lng = -74.09;
            x.Proveedor = "terpel";
            x.PrecioAcpm = 9100m;
        });
        await e.AgregarEstacionAsync($"DISTRACOM {e.Marca} SUR", x =>
        {
            x.Lat = 4.3;
            x.Lng = -74.0;
            x.Proveedor = "distracom";
        });

        await e.AgregarTanqueoAsync(placa, new DateTimeOffset(2026, 6, 10, 10, 0, 0, BOGOTA), 40m, 9300m, $"EDS {e.Marca}");
        await e.AgregarTanqueoAsync(placa, new DateTimeOffset(2026, 6, 20, 8, 0, 0, BOGOTA), 50m, 9500m, $"{e.Marca} SUR", FuenteTanqueo.Distracom);
        // Anterior al viaje (no hay viaje vigente en esa fecha): cuenta como tanqueo pero sin referencia de ruta.
        await e.AgregarTanqueoAsync(placa, new DateTimeOffset(2026, 5, 15, 8, 0, 0, BOGOTA), 10m, 10000m, "EDS Desconocida");
        return new Datos(e, placa, ruta, viaje.Id, eds.Id);
    }

    [clsHechoConBaseDePruebas]
    public async Task Liga_cada_tanqueo_con_su_viaje_su_ruta_su_referencia_de_precio_y_su_estacion()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var d = await SembrarAsync(e);
        var repo = new clsSqlMetricasRepository(e.Db);

        var filas = await repo.ListarTanqueosAsync(new(null, null, null, null, null, d.Ruta), default);

        // Filtrando por la ruta solo quedan los dos tanqueos ligados al viaje.
        Assert.Equal(2, filas.Count);
        var terpel = filas.Single(f => f.Proveedor == "terpel");
        Assert.Equal(d.Placa, terpel.Placa);
        Assert.Equal(d.Ruta, terpel.Ruta);
        Assert.Equal(40m, terpel.Galones);
        Assert.Equal(9300m, terpel.PrecioPagado);
        Assert.Equal(9000m, terpel.AcpmMin);
        Assert.Equal(10000m, terpel.AcpmMax);
        Assert.Equal(d.EdsId, terpel.EstacionCatalogo!.Id);
        Assert.Equal(9100m, terpel.EstacionCatalogo.PrecioAcpm);
        Assert.Equal(4.65, terpel.EstacionCatalogo.Lat, 6);
        Assert.Equal(4.6, terpel.ExtremosRuta!.OrigenLat, 6);
        Assert.Equal(-73.6, terpel.ExtremosRuta.DestinoLng, 6);

        // El historial Distracom trae "<NOMBRE>" y el catalogo "DISTRACOM <NOMBRE>": tambien se casa.
        var distracom = filas.Single(f => f.Proveedor == "distracom");
        Assert.Equal("distracom", distracom.EstacionCatalogo!.Proveedor);
    }

    [clsHechoConBaseDePruebas]
    public async Task Tanqueo_fuera_de_la_ventana_del_viaje_queda_sin_ruta_ni_referencia()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var d = await SembrarAsync(e);
        var repo = new clsSqlMetricasRepository(e.Db);

        var filas = await repo.ListarTanqueosAsync(new(null, null, null, null, 5, null), default);

        var anterior = filas.Single(f => f.Placa == d.Placa);
        Assert.Null(anterior.Ruta);
        Assert.Null(anterior.AcpmMin);
        Assert.Null(anterior.ExtremosRuta);
        Assert.Null(anterior.EstacionCatalogo);
    }

    [clsHechoConBaseDePruebas]
    public async Task Los_totales_via_handler_coinciden_con_el_calculo_a_mano()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var d = await SembrarAsync(e);
        var handler = new clsObtenerMetricasRecuperacionQueryHandler(new clsSqlMetricasRepository(e.Db));

        var dto = await handler.Handle(new clsObtenerMetricasRecuperacionQuery(null, null, null, null, null, d.Ruta));

        // 40 gal a 9.300 + 50 gal a 9.500 = 372.000 + 475.000
        Assert.Equal(90m, dto.GalonesTotal);
        Assert.Equal(2, dto.TanqueosTotal);
        Assert.Equal(847_000m, dto.GastoReal);
        // ideal = 90 x 9.000 ; maximo = 90 x 10.000
        Assert.Equal(810_000m, dto.GastoIdeal);
        Assert.Equal(900_000m, dto.GastoMaximo);
        Assert.Equal(53_000m, dto.AhorroVsMaximo);
        Assert.Equal(37_000m, dto.OportunidadVsIdeal);
        Assert.Equal(9000m, dto.PrecioMinimoRuta);
        Assert.Equal(2, dto.GalonesPorEds.Count);
        Assert.Equal(50m, dto.GalonesPorEds[0].Galones);   // la de mas volumen primero
        var ruta = Assert.Single(dto.Rutas);
        Assert.Equal(d.Ruta, ruta.Ruta);
        var equipo = Assert.Single(dto.Equipos);
        Assert.Equal(9300m, equipo.ValorGalon);
        Assert.Equal(d.Placa, equipo.Equipo);
        Assert.Equal(6, equipo.Mes);
    }

    [clsHechoConBaseDePruebas]
    public async Task Los_filtros_se_aplican_en_la_consulta()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var d = await SembrarAsync(e);
        var repo = new clsSqlMetricasRepository(e.Db);
        var programa = $"PROG-{e.Marca}";

        Assert.Equal(3, (await repo.ListarTanqueosAsync(new(programa, null, null, null, null, null), default)).Count);
        Assert.Empty(await repo.ListarTanqueosAsync(new($"OTRO-{e.Marca}", null, null, null, null, null), default));

        var terpel = await repo.ListarTanqueosAsync(new(programa, "terpel", null, null, null, null), default);
        Assert.Equal(2, terpel.Count);
        Assert.All(terpel, t => Assert.Equal("terpel", t.Proveedor));

        Assert.Equal(2, (await repo.ListarTanqueosAsync(new(programa, null, null, 2026, 6, null), default)).Count);
        Assert.Empty(await repo.ListarTanqueosAsync(new(programa, null, null, 2025, null, null), default));
        Assert.Equal(3, (await repo.ListarTanqueosAsync(new(programa, null, "diesel", null, null, null), default)).Count);
        Assert.Empty(await repo.ListarTanqueosAsync(new(programa, null, null, null, null, "Ruta que no existe"), default));
        Assert.NotEmpty(d.Ruta);
    }

    [clsHechoConBaseDePruebas]
    public async Task Solo_cuentan_los_tanqueos_de_diesel()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var d = await SembrarAsync(e);
        await e.AgregarTanqueoAsync(d.Placa, new DateTimeOffset(2026, 6, 11, 8, 0, 0, BOGOTA), 25m, 15000m, "EDS Gasolina", producto: "GASOLINA");
        var repo = new clsSqlMetricasRepository(e.Db);

        var filas = await repo.ListarTanqueosAsync(new($"PROG-{e.Marca}", null, null, null, null, null), default);

        Assert.Equal(3, filas.Count);   // los 3 de diesel; el de gasolina no
    }

    [clsHechoConBaseDePruebas]
    public async Task Los_valores_del_filtro_se_pasan_como_parametros_y_no_se_interpretan_como_SQL()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        await SembrarAsync(e);
        var repo = new clsSqlMetricasRepository(e.Db);

        var filas = await repo.ListarTanqueosAsync(new("x' OR '1'='1", "'; DROP TABLE tblTanqueos; --", null, null, null, "' OR 1=1 --"), default);

        Assert.Empty(filas);
        Assert.NotEmpty(await repo.ListarTanqueosAsync(Sin(), default));   // la tabla sigue ahi
    }

    [clsHechoConBaseDePruebas]
    public async Task Las_opciones_incluyen_los_valores_sembrados_ordenados()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var d = await SembrarAsync(e);
        var repo = new clsSqlMetricasRepository(e.Db);

        var o = await repo.ObtenerOpcionesAsync(default);

        Assert.Contains($"PROG-{e.Marca}", o.Programas);
        Assert.Contains("terpel", o.Proveedores);
        Assert.Contains("distracom", o.Proveedores);
        Assert.Contains(o.Productos, p => string.Equals(p, "diesel", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(2026, o.Anios);
        Assert.Contains(6, o.Meses);
        Assert.Contains(d.Ruta, o.Rutas);
        Assert.Equal(o.Anios.OrderByDescending(a => a), o.Anios);
        Assert.Equal(o.Meses.OrderBy(m => m), o.Meses);
        Assert.DoesNotContain(o.Productos, p => p.Equals("GASOLINA", StringComparison.OrdinalIgnoreCase));
    }
}
