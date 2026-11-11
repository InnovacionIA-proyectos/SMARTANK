using Smartank.Api.Tests.Infra;
using Smartank.Infrastructure.Estaciones.Repositorios;

namespace Smartank.Api.Tests.Estaciones;

/// <summary>Listado de estaciones contra SQL Server (se omite sin TEST_DB_CONNECTION). Cada prueba revierte sus datos.</summary>
public class clsEstacionesRepositoryTests
{
    [clsHechoConBaseDePruebas]
    public async Task Con_ruta_ordena_por_precio_ascendente_y_deja_las_estaciones_sin_precio_al_final()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ruta = $"ruta-{e.Marca}";
        await e.AgregarEstacionAsync("Sin precio", x => x.Ruta = ruta);
        await e.AgregarEstacionAsync("Cara", x => { x.Ruta = ruta; x.PrecioGalonActual = 9500m; });
        await e.AgregarEstacionAsync("Barata", x => { x.Ruta = ruta; x.PrecioGalonActual = 9000m; });
        await e.AgregarEstacionAsync("De otra ruta", x => { x.Ruta = "otra"; x.PrecioGalonActual = 1m; });
        var repo = new clsEfEstacionesRepository(e.Db);

        var lista = await repo.ListarAsync(ruta, default);

        Assert.Equal(new[] { "Barata", "Cara", "Sin precio" }, lista.Select(s => s.Nombre));
    }

    [clsHechoConBaseDePruebas]
    public async Task Sin_ruta_agrupa_por_ruta_y_por_precio_con_las_estaciones_sin_ruta_al_final()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var rutaA = $"aaa-{e.Marca}";
        var rutaB = $"bbb-{e.Marca}";
        await e.AgregarEstacionAsync("B2", x => { x.Ruta = rutaB; x.PrecioGalonActual = 9800m; });
        await e.AgregarEstacionAsync("A-sin-precio", x => x.Ruta = rutaA);
        await e.AgregarEstacionAsync("A1", x => { x.Ruta = rutaA; x.PrecioGalonActual = 9100m; });
        await e.AgregarEstacionAsync("B1", x => { x.Ruta = rutaB; x.PrecioGalonActual = 9200m; });
        await e.AgregarEstacionAsync($"Sin ruta {e.Marca}");
        var repo = new clsEfEstacionesRepository(e.Db);

        var lista = (await repo.ListarAsync(null, default))
            .Where(s => s.Ruta == rutaA || s.Ruta == rutaB || s.Nombre == $"Sin ruta {e.Marca}")
            .ToList();

        Assert.Equal(new[] { "A1", "A-sin-precio", "B1", "B2", $"Sin ruta {e.Marca}" }, lista.Select(s => s.Nombre));
    }

    [clsHechoConBaseDePruebas]
    public async Task Ruta_sin_estaciones_devuelve_lista_vacia()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var repo = new clsEfEstacionesRepository(e.Db);

        Assert.Empty(await repo.ListarAsync($"no-existe-{e.Marca}", default));
    }

    [clsHechoConBaseDePruebas]
    public async Task Devuelve_todos_los_campos_de_la_estacion()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ruta = $"campos-{e.Marca}";
        var fecha = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        await e.AgregarEstacionAsync("EDS completa", x =>
        {
            x.Ruta = ruta;
            x.Lat = 4.65;
            x.Lng = -74.09;
            x.PrecioGalonActual = 9300m;
            x.PrecioAcpm = 9300m;
            x.PrecioCorriente = 15000m;
            x.PrecioActualizadoAt = fecha;
            x.SoportaPreautorizacion = true;
            x.Proveedor = "terpel";
            x.Producto = "ACPM";
            x.CodigoExterno = "EDS0042";
        });
        var repo = new clsEfEstacionesRepository(e.Db);

        var s = Assert.Single(await repo.ListarAsync(ruta, default));

        Assert.Equal(4.65, s.Lat!.Value, 6);
        Assert.Equal(-74.09, s.Lng!.Value, 6);
        Assert.Equal(9300m, s.PrecioAcpm);
        Assert.Equal(15000m, s.PrecioCorriente);
        Assert.Equal(fecha, s.PrecioActualizadoAt);
        Assert.True(s.SoportaPreautorizacion);
        Assert.Equal("terpel", s.Proveedor);
        Assert.Equal("ACPM", s.Producto);
        Assert.Equal("EDS0042", s.CodigoExterno);
    }
}
