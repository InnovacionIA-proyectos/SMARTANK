using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsOrquestadorSincronizacionTests
{
    /// <summary>Ejecutor que registra el orden de inicio y fin y permite fallar o bloquear jobs concretos.</summary>
    private sealed class clsEjecutorFalso : IEjecutorJob
    {
        public ConcurrentQueue<string> Eventos { get; } = new();
        public Dictionary<string, Exception> Fallos { get; } = [];
        public Dictionary<string, clsResultadoSincronizacion> Resultados { get; } = [];
        public Dictionary<string, TaskCompletionSource> Compuertas { get; } = [];

        public async Task<clsResultadoSincronizacion> EjecutarAsync(string nombreJob, CancellationToken ct = default)
        {
            Eventos.Enqueue("ini:" + nombreJob);
            if (Compuertas.TryGetValue(nombreJob, out var compuerta)) await compuerta.Task.WaitAsync(ct);
            await Task.Yield();
            if (Fallos.TryGetValue(nombreJob, out var ex)) throw ex;
            Eventos.Enqueue("fin:" + nombreJob);
            return Resultados.TryGetValue(nombreJob, out var r) ? r : clsResultadoSincronizacion.Crear(1, 1, 0, 0, []);
        }

        public IReadOnlyList<string> Iniciados => Eventos.Where(e => e.StartsWith("ini:")).Select(e => e[4..]).ToList();
    }

    private sealed class clsBloqueoFalso : IBloqueoDistribuidoPort
    {
        public HashSet<string> Ocupados { get; } = [];
        public List<string> Adquiridos { get; } = [];
        public int Liberados;

        public Task<IAsyncDisposable?> IntentarAdquirirAsync(string recurso, CancellationToken ct = default)
        {
            if (Ocupados.Contains(recurso)) return Task.FromResult<IAsyncDisposable?>(null);
            Adquiridos.Add(recurso);
            return Task.FromResult<IAsyncDisposable?>(new clsLiberador(this));
        }

        private sealed class clsLiberador(clsBloqueoFalso padre) : IAsyncDisposable
        {
            public ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref padre.Liberados);
                return ValueTask.CompletedTask;
            }
        }
    }

    private readonly clsEjecutorFalso _ejecutor = new();
    private readonly clsBloqueoFalso _bloqueo = new();
    // 2026-03-10 20:00 UTC = 15:00 del 10 de marzo en Colombia.
    private readonly clsRelojFalso _reloj = new(new DateTimeOffset(2026, 3, 10, 20, 0, 0, TimeSpan.Zero));

    private clsOrquestadorSincronizacion Orquestador(bool diarioAlIniciar = true) => new(
        _ejecutor, _bloqueo, _reloj, Options.Create(new clsSyncOptions { DiarioAlIniciar = diarioAlIniciar }),
        NullLogger<clsOrquestadorSincronizacion>.Instance);

    private static int Pos(IReadOnlyList<string> lista, string nombre) => lista.ToList().IndexOf(nombre);

    // ---------- Grupos ----------

    [Fact]
    public async Task El_grupo_frecuente_corre_telemetria_tanqueos_y_la_cadena_viajes_luego_rutas_incremental()
    {
        var resultados = await Orquestador().EjecutarFrecuenteAsync(CancellationToken.None);

        Assert.NotNull(resultados);
        Assert.Equal(4, resultados!.Count);
        var ini = _ejecutor.Iniciados;
        Assert.Contains("telemetria", ini);
        Assert.Contains("tanqueos", ini);
        // dentro de la cadena, rutas espera a que viajes TERMINE
        var eventos = _ejecutor.Eventos.ToList();
        Assert.True(eventos.IndexOf("fin:viajes") < eventos.IndexOf("ini:rutas-incremental"));
    }

    [Fact]
    public async Task Los_jobs_independientes_del_frecuente_arrancan_en_paralelo()
    {
        foreach (var j in new[] { "telemetria", "tanqueos", "viajes" }) _ejecutor.Compuertas[j] = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var tarea = Orquestador().EjecutarFrecuenteAsync(CancellationToken.None);
        await Task.Delay(100);

        // los tres iniciaron aunque ninguno ha terminado
        Assert.Equivalent(new[] { "telemetria", "tanqueos", "viajes" }, _ejecutor.Iniciados);
        foreach (var c in _ejecutor.Compuertas.Values) c.SetResult();
        await tarea;
    }

    [Fact]
    public async Task El_grupo_diario_corre_en_el_orden_de_dependencia()
    {
        await Orquestador().EjecutarDiarioAsync(CancellationToken.None);

        Assert.Equal(
            ["vehiculos", "conductores", "viajes", "rutas", "precios", "precios-ruta", "rendimiento"],
            _ejecutor.Iniciados);
    }

    // ---------- Aislamiento ----------

    [Fact]
    public async Task Un_job_que_falla_no_impide_que_corran_los_demas_y_se_registra_como_fallido()
    {
        _ejecutor.Fallos["conductores"] = new InvalidOperationException("boom");

        var resultados = await Orquestador().EjecutarDiarioAsync(CancellationToken.None);

        Assert.Equal(7, resultados!.Count);
        Assert.Equal(7, _ejecutor.Iniciados.Count);
        var conductores = resultados.Single(r => r.Nombre == "conductores");
        Assert.Equal(EstadoJob.Fallido, conductores.Estado);
        Assert.Equal("boom", conductores.Detalle);
        Assert.All(resultados.Where(r => r.Nombre != "conductores"), r => Assert.Equal(EstadoJob.Ok, r.Estado));
    }

    [Fact]
    public async Task Un_job_sin_configuracion_se_omite_con_aviso_y_no_es_fallo()
    {
        _ejecutor.Fallos["telemetria"] = new clsServiceUnavailableException("GEOTAB_NO_CONFIGURADO", "falta Geotab");

        var resultados = await Orquestador().EjecutarFrecuenteAsync(CancellationToken.None);

        var telemetria = resultados!.Single(r => r.Nombre == "telemetria");
        Assert.Equal(EstadoJob.Omitido, telemetria.Estado);
        Assert.Contains("GEOTAB_NO_CONFIGURADO", telemetria.Detalle);
        Assert.Equal(0, clsOrquestadorSincronizacion.CalcularCodigoSalida("frecuente", resultados!));
    }

    [Fact]
    public async Task Un_job_con_errores_de_registros_queda_ConErrores_con_sus_contadores()
    {
        _ejecutor.Resultados["viajes"] = clsResultadoSincronizacion.Crear(10, 8, 0, 0, [new clsErrorSincronizacion("FO 1", "dup"), new clsErrorSincronizacion("FO 2", "dup")]);

        var resultados = await Orquestador().EjecutarFrecuenteAsync(CancellationToken.None);

        var viajes = resultados!.Single(r => r.Nombre == "viajes");
        Assert.Equal(EstadoJob.ConErrores, viajes.Estado);
        Assert.Equal(2, viajes.Resultado!.ErroresTotal);
        Assert.True(viajes.Duracion >= TimeSpan.Zero);
        // y la cadena sigue: rutas-incremental corrio despues
        Assert.Contains("rutas-incremental", _ejecutor.Iniciados);
    }

    [Fact]
    public async Task La_cancelacion_del_apagado_deja_el_job_como_cancelado_y_corta_la_cadena()
    {
        using var cts = new CancellationTokenSource();
        _ejecutor.Compuertas["vehiculos"] = new(TaskCreationOptions.RunContinuationsAsynchronously);

        var tarea = Orquestador().EjecutarDiarioAsync(cts.Token);
        await Task.Delay(50);
        cts.Cancel();
        var resultados = await tarea;

        Assert.Equal(EstadoJob.Cancelado, Assert.Single(resultados!).Estado);
        Assert.Equal(["vehiculos"], _ejecutor.Iniciados);
    }

    // ---------- No solapamiento ----------

    [Fact]
    public async Task Un_grupo_no_se_solapa_consigo_mismo_dentro_del_proceso()
    {
        _ejecutor.Compuertas["vehiculos"] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var o = Orquestador();

        var primera = o.EjecutarDiarioAsync(CancellationToken.None);
        await Task.Delay(50);
        var segunda = await o.EjecutarDiarioAsync(CancellationToken.None);

        Assert.Null(segunda);                                     // omitida: la anterior sigue en curso
        Assert.Single(_bloqueo.Adquiridos);                       // ni siquiera pidio el bloqueo distribuido
        _ejecutor.Compuertas["vehiculos"].SetResult();
        Assert.NotNull(await primera);
    }

    [Fact]
    public async Task Los_grupos_frecuente_y_diario_si_pueden_correr_a_la_vez()
    {
        _ejecutor.Compuertas["vehiculos"] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var o = Orquestador();

        var diario = o.EjecutarDiarioAsync(CancellationToken.None);
        await Task.Delay(50);
        var frecuente = await o.EjecutarFrecuenteAsync(CancellationToken.None);

        Assert.NotNull(frecuente);
        _ejecutor.Compuertas["vehiculos"].SetResult();
        await diario;
    }

    [Fact]
    public async Task Despues_de_terminar_el_grupo_puede_volver_a_correr()
    {
        var o = Orquestador();

        Assert.NotNull(await o.EjecutarFrecuenteAsync(CancellationToken.None));
        Assert.NotNull(await o.EjecutarFrecuenteAsync(CancellationToken.None));
    }

    // ---------- Bloqueo entre replicas ----------

    [Fact]
    public async Task Si_otra_replica_tiene_el_bloqueo_el_grupo_se_omite_sin_ejecutar_ningun_job()
    {
        _bloqueo.Ocupados.Add("smartank:sync:frecuente");

        var resultados = await Orquestador().EjecutarFrecuenteAsync(CancellationToken.None);

        Assert.Null(resultados);
        Assert.Empty(_ejecutor.Iniciados);
    }

    [Fact]
    public async Task El_bloqueo_es_por_grupo_y_se_libera_al_terminar_aun_si_un_job_fallo()
    {
        _bloqueo.Ocupados.Add("smartank:sync:frecuente");
        _ejecutor.Fallos["vehiculos"] = new InvalidOperationException("x");

        await Orquestador().EjecutarDiarioAsync(CancellationToken.None);

        Assert.Equal(["smartank:sync:diario"], _bloqueo.Adquiridos);
        Assert.Equal(1, _bloqueo.Liberados);
    }

    [Fact]
    public async Task Si_el_bloqueo_distribuido_falla_el_grupo_se_omite_sin_lanzar()
    {
        var o = new clsOrquestadorSincronizacion(
            _ejecutor, new clsBloqueoQueFalla(), _reloj, Options.Create(new clsSyncOptions()), NullLogger<clsOrquestadorSincronizacion>.Instance);

        var resultados = await o.EjecutarFrecuenteAsync(CancellationToken.None);

        Assert.Null(resultados);
        Assert.Empty(_ejecutor.Iniciados);
    }

    private sealed class clsBloqueoQueFalla : IBloqueoDistribuidoPort
    {
        public Task<IAsyncDisposable?> IntentarAdquirirAsync(string recurso, CancellationToken ct = default) =>
            throw new InvalidOperationException("sin base");
    }

    // ---------- Una vez por dia ----------

    [Fact]
    public void El_diario_corre_al_iniciar_y_no_se_repite_el_mismo_dia_de_Colombia()
    {
        var o = Orquestador();

        Assert.True(o.DebeCorrerDiario());
        Assert.False(o.DebeCorrerDiario());
        _reloj.Avanzar(TimeSpan.FromHours(8));         // 23:00 del 10 en Colombia: sigue siendo el mismo dia
        Assert.False(o.DebeCorrerDiario());
    }

    [Fact]
    public void El_diario_vuelve_a_correr_al_cambiar_el_dia_calendario_de_Colombia_no_el_de_UTC()
    {
        var o = Orquestador();
        Assert.True(o.DebeCorrerDiario());               // 15:00 del 10 (Colombia)

        _reloj.Avanzar(TimeSpan.FromHours(8));           // 04:00Z del 11 = 23:00 del 10 en Colombia
        Assert.False(o.DebeCorrerDiario());              // UTC ya cambio de dia, Colombia no

        _reloj.Avanzar(TimeSpan.FromHours(1));           // 05:00Z del 11 = 00:00 del 11 en Colombia
        Assert.True(o.DebeCorrerDiario());
        Assert.False(o.DebeCorrerDiario());
    }

    [Fact]
    public void Sin_DiarioAlIniciar_el_primer_tick_no_corre_el_diario_y_espera_al_proximo_dia()
    {
        var o = Orquestador(diarioAlIniciar: false);

        Assert.False(o.DebeCorrerDiario());
        Assert.False(o.DebeCorrerDiario());
        _reloj.Avanzar(TimeSpan.FromHours(10));          // 01:00 del 11 en Colombia
        Assert.True(o.DebeCorrerDiario());
    }

    [Fact]
    public async Task IniciarTick_lanza_siempre_el_frecuente_y_el_diario_solo_cuando_toca()
    {
        var o = Orquestador();

        var primero = o.IniciarTick(CancellationToken.None);
        await Task.WhenAll(primero);
        var segundo = o.IniciarTick(CancellationToken.None);
        await Task.WhenAll(segundo);

        Assert.Equal(2, primero.Count);   // frecuente + diario
        Assert.True(segundo.Count == 1, "solo el frecuente");
    }

    // ---------- Modo manual ----------

    [Fact]
    public async Task EjecutarPorNombre_corre_un_job_individual_sin_bloqueos()
    {
        var resultados = await Orquestador().EjecutarPorNombreAsync("rutas-incremental", CancellationToken.None);

        Assert.Equal("rutas-incremental", Assert.Single(resultados).Nombre);
        Assert.Empty(_bloqueo.Adquiridos);
    }

    [Fact]
    public async Task EjecutarPorNombre_con_un_grupo_usa_su_bloqueo_y_con_un_nombre_desconocido_lanza()
    {
        var o = Orquestador();

        var diario = await o.EjecutarPorNombreAsync("diario", CancellationToken.None);
        Assert.Equal(7, diario.Count);
        Assert.Contains("smartank:sync:diario", _bloqueo.Adquiridos);

        await Assert.ThrowsAsync<ArgumentException>(() => o.EjecutarPorNombreAsync("nada", CancellationToken.None));
    }

    [Fact]
    public void El_codigo_de_salida_es_1_ante_cualquier_fallo_y_para_un_individual_tambien_ante_omitido_o_errores()
    {
        static clsResultadoJob R(string n, EstadoJob e) => new(n, e, TimeSpan.Zero, null, null);

        Assert.Equal(0, clsOrquestadorSincronizacion.CalcularCodigoSalida("viajes", [R("viajes", EstadoJob.Ok)]));
        Assert.Equal(1, clsOrquestadorSincronizacion.CalcularCodigoSalida("viajes", [R("viajes", EstadoJob.Fallido)]));
        Assert.Equal(1, clsOrquestadorSincronizacion.CalcularCodigoSalida("viajes", [R("viajes", EstadoJob.ConErrores)]));
        Assert.Equal(1, clsOrquestadorSincronizacion.CalcularCodigoSalida("viajes", [R("viajes", EstadoJob.Omitido)]));
        Assert.Equal(0, clsOrquestadorSincronizacion.CalcularCodigoSalida("diario", [R("a", EstadoJob.Ok), R("b", EstadoJob.Omitido)]));
        Assert.Equal(1, clsOrquestadorSincronizacion.CalcularCodigoSalida("diario", [R("a", EstadoJob.Ok), R("b", EstadoJob.Fallido)]));
    }

    [Fact]
    public void Los_nombres_de_job_validos_son_los_del_modo_manual()
    {
        Assert.Equal(
            ["vehiculos", "conductores", "viajes", "rutas", "rutas-incremental", "tanqueos", "telemetria", "precios", "precios-ruta", "rendimiento", "frecuente", "diario"],
            clsNombresJob.Todos);
        Assert.True(clsNombresJob.EsValido("precios-ruta"));
        Assert.False(clsNombresJob.EsValido("Precios"));
        Assert.False(clsNombresJob.EsValido(null));
    }
}
