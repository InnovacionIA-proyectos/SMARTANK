using Smartank.Api.Tests.Infra;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Domain.Enums;
using Smartank.Domain.Viajes.Reglas;
using Smartank.Infrastructure.Viajes.Repositorios;

namespace Smartank.Api.Tests.Viajes;

/// <summary>Consultas de lectura de viajes contra SQL Server (se omite sin TEST_DB_CONNECTION). Cada prueba revierte sus datos.</summary>
public class clsViajesLecturaRepositoryTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private static clsViajesFiltro Filtro(
        EstadoViaje? estado = null, string? ruta = null, string? placa = null, string? manifiesto = null, int? anio = null, int? mes = null,
        TipoDesplazamiento? desplazamiento = null, bool recomendacion = false, bool? telemetria = null) =>
        new(estado, ruta, placa, manifiesto, anio, mes, desplazamiento, recomendacion, telemetria, AHORA.AddHours(-1));

    private static readonly double[][] POLILINEA = [[4.6, -74.1], [4.4, -73.9], [4.15, -73.6]];

    [clsHechoConBaseDePruebas]
    public async Task Lista_los_viajes_no_cerrados_con_vehiculo_ruta_y_recomendacion_activa()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ruta = $"RUTA-{e.Marca}";
        var ve = await e.AgregarVehiculoAsync(e.Placa(1), v =>
        {
            v.NivelTanqueActual = 40m;
            v.RendimientoHistorico = 8.5;
            v.CapacidadTanqueGal = 150m;
            v.UltimaLat = 4.55;
            v.UltimaLng = -74.0;
            v.NivelAt = AHORA.AddMinutes(-10);
        });
        var viaje = await e.AgregarViajeAsync(ve.Id, ruta, v =>
        {
            v.Estado = EstadoViaje.EnRuta;
            v.Manifiesto = "1234567890";
            v.InicioAt = new DateTimeOffset(2026, 6, 14, 6, 0, 0, TimeSpan.Zero);
            v.NombreRuta = "BOGOTA - VILLAVICENCIO";
        });
        await e.AgregarRutaAsync(viaje.Id, POLILINEA, 666);
        var eds = await e.AgregarEstacionAsync($"EDS {e.Marca}");
        await e.AgregarAutorizacionAsync(viaje.Id, ve.Id, eds.Id, EstadoAutorizacion.Propuesta);
        var cerrado = await e.AgregarViajeAsync(ve.Id, ruta, v => v.Estado = EstadoViaje.Cerrado);
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var filas = await repo.ListarAsync(Filtro(ruta: ruta), default);

        var fila = Assert.Single(filas);
        Assert.Equal(viaje.Id, fila.Id);
        Assert.DoesNotContain(filas, f => f.Id == cerrado.Id);
        Assert.Equal(e.Placa(1), fila.Placa);
        Assert.Equal("BOGOTA - VILLAVICENCIO", fila.NombreRuta);
        Assert.Equal(EstadoViaje.EnRuta, fila.Estado);
        Assert.Equal(40m, fila.NivelTanqueActual);
        Assert.Equal(150m, fila.CapacidadTanqueGal);
        Assert.Equal(8.5, fila.RendimientoHistorico);
        Assert.Equal(0.02, fila.ToleranciaMotor, 6);
        Assert.Equal(4.55, fila.VehiculoLat!.Value, 6);
        Assert.Equal(666, fila.RutaKm);
        // Origen y destino los deriva el trigger de la base del primer y el ultimo punto de la polilinea.
        Assert.Equal(4.6, fila.OrigenLat!.Value, 6);
        Assert.Equal(-74.1, fila.OrigenLng!.Value, 6);
        Assert.Equal(4.15, fila.DestinoLat!.Value, 6);
        Assert.Equal(-73.6, fila.DestinoLng!.Value, 6);
        Assert.True(fila.RecomendacionActiva);
    }

    [clsHechoConBaseDePruebas]
    public async Task Viaje_sin_ruta_ni_autorizacion_trae_nulos_y_sin_recomendacion()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ruta = $"SIN-{e.Marca}";
        var ve = await e.AgregarVehiculoAsync(e.Placa(2));
        await e.AgregarViajeAsync(ve.Id, ruta);
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var fila = Assert.Single(await repo.ListarAsync(Filtro(ruta: ruta), default));

        Assert.Null(fila.RutaKm);
        Assert.Null(fila.OrigenLat);
        Assert.Null(fila.DestinoLng);
        Assert.False(fila.RecomendacionActiva);
        Assert.Null(fila.NivelTanqueActual);
    }

    [clsHechoConBaseDePruebas]
    public async Task Autorizacion_ejecutada_no_cuenta_como_recomendacion_activa_y_autorizada_si()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(3));
        var eds = await e.AgregarEstacionAsync($"EDS {e.Marca}");
        var vEjecutada = await e.AgregarViajeAsync(ve.Id, $"EJE-{e.Marca}");
        var vAutorizada = await e.AgregarViajeAsync(ve.Id, $"AUT-{e.Marca}");
        await e.AgregarAutorizacionAsync(vEjecutada.Id, ve.Id, eds.Id, EstadoAutorizacion.Ejecutada);
        await e.AgregarAutorizacionAsync(vAutorizada.Id, ve.Id, eds.Id, EstadoAutorizacion.Autorizada);
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var todas = await repo.ListarAsync(Filtro(placa: e.Marca), default);
        var soloConRecomendacion = await repo.ListarAsync(Filtro(placa: e.Marca, recomendacion: true), default);

        Assert.Equal(2, todas.Count);
        Assert.False(todas.Single(f => f.Id == vEjecutada.Id).RecomendacionActiva);
        Assert.True(todas.Single(f => f.Id == vAutorizada.Id).RecomendacionActiva);
        Assert.Equal(vAutorizada.Id, Assert.Single(soloConRecomendacion).Id);
    }

    [clsHechoConBaseDePruebas]
    public async Task Filtra_por_estado_ruta_placa_y_manifiesto()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve1 = await e.AgregarVehiculoAsync(e.Placa(10));
        var ve2 = await e.AgregarVehiculoAsync(e.Placa(20));
        var programado = await e.AgregarViajeAsync(ve1.Id, $"A-{e.Marca}", v => { v.Estado = EstadoViaje.Programado; v.Manifiesto = "MAN-AAA-1"; });
        var enRuta = await e.AgregarViajeAsync(ve2.Id, $"B-{e.Marca}", v => { v.Estado = EstadoViaje.EnRuta; v.Manifiesto = "MAN-BBB-2"; });
        var repo = new clsEfViajesLecturaRepository(e.Db);

        Assert.Equal(enRuta.Id, Assert.Single(await repo.ListarAsync(Filtro(estado: EstadoViaje.EnRuta, placa: e.Marca), default)).Id);
        Assert.Equal(programado.Id, Assert.Single(await repo.ListarAsync(Filtro(ruta: $"A-{e.Marca}"), default)).Id);
        // Placa: contiene, sin distinguir mayusculas.
        Assert.Equal(enRuta.Id, Assert.Single(await repo.ListarAsync(Filtro(placa: e.Placa(20).ToLowerInvariant()), default)).Id);
        // Manifiesto: contiene.
        Assert.Equal(programado.Id, Assert.Single(await repo.ListarAsync(Filtro(placa: e.Marca, manifiesto: "AAA"), default)).Id);
        Assert.Empty(await repo.ListarAsync(Filtro(placa: e.Marca, manifiesto: "no-existe"), default));
    }

    [clsHechoConBaseDePruebas]
    public async Task Los_caracteres_comodin_del_usuario_no_se_interpretan_como_patron()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(30));
        await e.AgregarViajeAsync(ve.Id, $"W-{e.Marca}", v => v.Manifiesto = "ABC123");
        var repo = new clsEfViajesLecturaRepository(e.Db);

        // "%" y "_" son comodines de LIKE: buscados como texto no deben traer todo.
        Assert.Empty(await repo.ListarAsync(Filtro(placa: e.Marca, manifiesto: "%"), default));
        Assert.Empty(await repo.ListarAsync(Filtro(placa: e.Marca, manifiesto: "A_C"), default));
        Assert.Single(await repo.ListarAsync(Filtro(placa: e.Marca, manifiesto: "BC1"), default));
    }

    [clsHechoConBaseDePruebas]
    public async Task Desplazamiento_vacio_es_manifiesto_de_10_caracteres_que_empieza_con_32()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(40));
        var vacio = await e.AgregarViajeAsync(ve.Id, $"V-{e.Marca}", v => v.Manifiesto = "3200000001");
        var cargado = await e.AgregarViajeAsync(ve.Id, $"C-{e.Marca}", v => v.Manifiesto = "4500000001");
        var corto = await e.AgregarViajeAsync(ve.Id, $"K-{e.Marca}", v => v.Manifiesto = "320000001");
        var sinManifiesto = await e.AgregarViajeAsync(ve.Id, $"S-{e.Marca}");
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var vacios = await repo.ListarAsync(Filtro(placa: e.Marca, desplazamiento: TipoDesplazamiento.Vacio), default);
        var cargados = await repo.ListarAsync(Filtro(placa: e.Marca, desplazamiento: TipoDesplazamiento.Cargado), default);

        Assert.Equal(vacio.Id, Assert.Single(vacios).Id);
        Assert.Equivalent(new[] { cargado.Id, corto.Id, sinManifiesto.Id }, cargados.Select(f => f.Id));
    }

    [clsHechoConBaseDePruebas]
    public async Task Filtra_por_anio_y_mes_de_la_fecha_efectiva_inicio_real_o_despacho()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(50));
        // Con inicio real manda el inicio; sin el, la fecha de despacho.
        var junio2026 = await e.AgregarViajeAsync(ve.Id, $"J-{e.Marca}", v =>
        {
            v.InicioAt = new DateTimeOffset(2026, 6, 14, 6, 0, 0, TimeSpan.Zero);
            v.FechaDespacho = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        });
        var marzo2025 = await e.AgregarViajeAsync(ve.Id, $"M-{e.Marca}", v => v.FechaDespacho = new DateTimeOffset(2025, 3, 10, 0, 0, 0, TimeSpan.Zero));
        await e.AgregarViajeAsync(ve.Id, $"N-{e.Marca}");
        var repo = new clsEfViajesLecturaRepository(e.Db);

        Assert.Equal(junio2026.Id, Assert.Single(await repo.ListarAsync(Filtro(placa: e.Marca, anio: 2026), default)).Id);
        Assert.Equal(marzo2025.Id, Assert.Single(await repo.ListarAsync(Filtro(placa: e.Marca, anio: 2025), default)).Id);
        Assert.Equal(marzo2025.Id, Assert.Single(await repo.ListarAsync(Filtro(placa: e.Marca, anio: 2025, mes: 3), default)).Id);
        Assert.Empty(await repo.ListarAsync(Filtro(placa: e.Marca, anio: 2025, mes: 6), default));
        Assert.Equal(junio2026.Id, Assert.Single(await repo.ListarAsync(Filtro(placa: e.Marca, mes: 6), default)).Id);
    }

    [clsHechoConBaseDePruebas]
    public async Task Filtra_por_telemetria_reciente_segun_la_ultima_lectura_de_nivel()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var reciente = await e.AgregarVehiculoAsync(e.Placa(60), v => v.NivelAt = AHORA.AddMinutes(-30));
        var vieja = await e.AgregarVehiculoAsync(e.Placa(61), v => v.NivelAt = AHORA.AddHours(-3));
        var sinLectura = await e.AgregarVehiculoAsync(e.Placa(62));
        var vReciente = await e.AgregarViajeAsync(reciente.Id, $"R-{e.Marca}");
        var vVieja = await e.AgregarViajeAsync(vieja.Id, $"V-{e.Marca}");
        var vSin = await e.AgregarViajeAsync(sinLectura.Id, $"S-{e.Marca}");
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var si = await repo.ListarAsync(Filtro(placa: e.Marca, telemetria: true), default);
        var no = await repo.ListarAsync(Filtro(placa: e.Marca, telemetria: false), default);
        var todos = await repo.ListarAsync(Filtro(placa: e.Marca), default);

        Assert.Equal(vReciente.Id, Assert.Single(si).Id);
        Assert.Equivalent(new[] { vVieja.Id, vSin.Id }, no.Select(f => f.Id));
        Assert.Equal(3, todos.Count);
    }

    [clsHechoConBaseDePruebas]
    public async Task Ordena_del_viaje_mas_reciente_al_mas_antiguo()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(70));
        var primero = await e.AgregarViajeAsync(ve.Id, $"P-{e.Marca}");
        await Task.Delay(30);
        var segundo = await e.AgregarViajeAsync(ve.Id, $"Q-{e.Marca}");
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var filas = await repo.ListarAsync(Filtro(placa: e.Marca), default);

        Assert.Equal(new[] { segundo.Id, primero.Id }, filas.Select(f => f.Id));
    }

    [clsHechoConBaseDePruebas]
    public async Task Opciones_listan_rutas_y_anios_de_los_viajes_activos_ordenados()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(80));
        await e.AgregarViajeAsync(ve.Id, $"ZZ-{e.Marca}", v => v.FechaDespacho = new DateTimeOffset(2031, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await e.AgregarViajeAsync(ve.Id, $"AA-{e.Marca}", v => v.FechaDespacho = new DateTimeOffset(2030, 2, 1, 0, 0, 0, TimeSpan.Zero));
        await e.AgregarViajeAsync(ve.Id, $"CERRADO-{e.Marca}", v =>
        {
            v.Estado = EstadoViaje.Cerrado;
            v.FechaDespacho = new DateTimeOffset(2040, 2, 1, 0, 0, 0, TimeSpan.Zero);
        });
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var opciones = await repo.ObtenerOpcionesAsync(default);

        Assert.Contains($"AA-{e.Marca}", opciones.Rutas);
        Assert.Contains($"ZZ-{e.Marca}", opciones.Rutas);
        Assert.DoesNotContain($"CERRADO-{e.Marca}", opciones.Rutas);
        // Rutas en orden alfabetico.
        Assert.True(opciones.Rutas.ToList().IndexOf($"AA-{e.Marca}") < opciones.Rutas.ToList().IndexOf($"ZZ-{e.Marca}"));
        Assert.DoesNotContain(2040, opciones.Anios);
        Assert.Contains(2031, opciones.Anios);
        Assert.Contains(2030, opciones.Anios);
        // Anios de mayor a menor.
        Assert.Equal(opciones.Anios.OrderByDescending(a => a), opciones.Anios);
    }

    [clsHechoConBaseDePruebas]
    public async Task ObtenerRuta_devuelve_la_polilinea_completa_y_la_posicion_del_vehiculo()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(90), v =>
        {
            v.UltimaLat = 4.5;
            v.UltimaLng = -74.0;
            v.UbicacionAt = AHORA.AddMinutes(-5);
            v.NivelTanqueActual = 40m;
        });
        var viaje = await e.AgregarViajeAsync(ve.Id, $"RUTA-{e.Marca}", v => v.Estado = EstadoViaje.EnRuta);
        await e.AgregarRutaAsync(viaje.Id, POLILINEA, 666);
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var fila = await repo.ObtenerRutaAsync(viaje.Id, default);

        Assert.NotNull(fila);
        Assert.Equal(viaje.Id, fila!.Id);
        Assert.Equal(EstadoViaje.EnRuta, fila.Estado);
        Assert.Equal(e.Placa(90), fila.Placa);
        Assert.Equal(POLILINEA, fila.Polilinea);
        Assert.Equal(666, fila.RutaKm);
        Assert.Equal(3, fila.NumPuntos);
        Assert.Equal(4.5, fila.VehiculoLat!.Value, 6);
        Assert.Equal(AHORA.AddMinutes(-5), fila.VehiculoUbicacionAt);
        Assert.Equal(40m, fila.NivelTanqueActual);
    }

    [clsHechoConBaseDePruebas]
    public async Task ObtenerRuta_de_un_viaje_sin_ruta_trae_polilinea_nula_y_de_uno_inexistente_null()
    {
        await using var e = await clsEscenarioBd.CrearAsync();
        var ve = await e.AgregarVehiculoAsync(e.Placa(91));
        var viaje = await e.AgregarViajeAsync(ve.Id, $"RUTA-{e.Marca}");
        var repo = new clsEfViajesLecturaRepository(e.Db);

        var fila = await repo.ObtenerRutaAsync(viaje.Id, default);

        Assert.NotNull(fila);
        Assert.Null(fila!.Polilinea);
        Assert.Null(fila.RutaKm);
        Assert.Null(await repo.ObtenerRutaAsync(Guid.NewGuid(), default));
    }
}
