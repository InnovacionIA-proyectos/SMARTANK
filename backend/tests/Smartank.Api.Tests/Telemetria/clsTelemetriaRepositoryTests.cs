using Smartank.Api.Tests.Infra;
using Smartank.Infrastructure.Telemetria.Repositorios;

namespace Smartank.Api.Tests.Telemetria;

/// <summary>Repositorio de telemetria contra SQL Server (se omite sin TEST_DB_CONNECTION). Cada prueba revierte sus datos.</summary>
public class clsTelemetriaRepositoryTests
{
    private static readonly DateTimeOffset LECTURA = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

    [clsHechoConBaseDePruebas]
    public async Task Actualiza_nivel_odometro_y_posicion_en_un_solo_update()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var placa = e.Placa(1);
        await e.AgregarVehiculoAsync(placa);
        var repo = new clsEfTelemetriaRepository(e.Db);

        var v = await repo.ActualizarTelemetriaAsync(placa, 45.5m, 152800, 4.6, -74.1, LECTURA, default);

        Assert.NotNull(v);
        Assert.Equal(45.5m, v!.NivelTanqueActual);
        Assert.Equal(152800, v.Odometro);
        Assert.Equal(4.6, v.UltimaLat!.Value, 6);
        Assert.Equal(-74.1, v.UltimaLng!.Value, 6);
        Assert.Equal(LECTURA, v.NivelAt);
        Assert.Equal(LECTURA, v.UbicacionAt);
    }

    [clsHechoConBaseDePruebas]
    public async Task Los_datos_que_no_vienen_conservan_su_valor_anterior()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var placa = e.Placa(2);
        await e.AgregarVehiculoAsync(placa, v =>
        {
            v.Odometro = 1000;
            v.UltimaLat = 5.5;
            v.UltimaLng = -73.3;
            v.UbicacionAt = LECTURA.AddDays(-1);
        });
        var repo = new clsEfTelemetriaRepository(e.Db);

        // Solo nivel: no se pierde el odometro ni la posicion, y la fecha de posicion no cambia.
        var v = await repo.ActualizarTelemetriaAsync(placa, 12m, null, null, null, LECTURA, default);

        Assert.Equal(12m, v!.NivelTanqueActual);
        Assert.Equal(1000, v.Odometro);
        Assert.Equal(5.5, v.UltimaLat!.Value, 6);
        Assert.Equal(-73.3, v.UltimaLng!.Value, 6);
        Assert.Equal(LECTURA, v.NivelAt);
        Assert.Equal(LECTURA.AddDays(-1), v.UbicacionAt);
    }

    [clsHechoConBaseDePruebas]
    public async Task Placa_inexistente_devuelve_null()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var repo = new clsEfTelemetriaRepository(e.Db);

        Assert.Null(await repo.ActualizarTelemetriaAsync("ZZZ999", 1m, null, null, null, LECTURA, default));
    }

    [clsHechoConBaseDePruebas]
    public async Task La_placa_se_compara_sin_distinguir_mayusculas()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var placa = e.Placa(3);
        await e.AgregarVehiculoAsync(placa);
        var repo = new clsEfTelemetriaRepository(e.Db);

        Assert.NotNull(await repo.ActualizarTelemetriaAsync(placa.ToLowerInvariant(), 5m, null, null, null, LECTURA, default));
    }

    [clsHechoConBaseDePruebas]
    public async Task ObtenerVehiculo_lee_por_id_y_devuelve_null_si_no_existe()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var v = await e.AgregarVehiculoAsync(e.Placa(4), x => x.NivelTanqueActual = 30m);
        var repo = new clsEfTelemetriaRepository(e.Db);

        Assert.Equal(30m, (await repo.ObtenerVehiculoAsync(v.Id, default))!.NivelTanqueActual);
        Assert.Null(await repo.ObtenerVehiculoAsync(Guid.NewGuid(), default));
    }
}
