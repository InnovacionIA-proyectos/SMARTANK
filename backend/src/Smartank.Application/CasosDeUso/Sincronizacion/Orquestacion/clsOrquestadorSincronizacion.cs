using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

/// <summary>
/// Logica de la agenda de sincronizacion (replica de scheduler.mjs del origen), sin temporizadores ni procesos: el Worker
/// solo llama a <see cref="IniciarTick"/>. Reglas:
/// <list type="bullet">
/// <item>Grupo FRECUENTE: telemetria + tanqueos + la cadena viajes -> rutas incremental, en paralelo entre si y secuencial dentro de la cadena.</item>
/// <item>Grupo DIARIO: vehiculos, conductores, viajes, rutas, precios, precios por ruta y rendimiento, en ese orden, una vez por dia calendario de Colombia (y al arrancar si <c>Sync__DiarioAlIniciar</c>).</item>
/// <item>Cada job esta aislado: un fallo se registra con su duracion y no afecta a los demas.</item>
/// <item>Un mismo grupo no se solapa ni en el proceso (semaforo) ni entre replicas (bloqueo distribuido).</item>
/// </list>
/// </summary>
public sealed class clsOrquestadorSincronizacion(
    IEjecutorJob ejecutor,
    IBloqueoDistribuidoPort bloqueo,
    TimeProvider reloj,
    IOptions<clsSyncOptions> opciones,
    ILogger<clsOrquestadorSincronizacion> log)
{
    private const string PREFIJO_BLOQUEO = "smartank:sync:";

    private readonly SemaphoreSlim _frecuente = new(1, 1);
    private readonly SemaphoreSlim _diario = new(1, 1);
    private readonly object _candadoDia = new();
    private bool _diaInicializado;
    private DateOnly? _ultimoDiaDiario;

    /// <summary>
    /// Decide si en este tick toca el grupo diario: la primera vez (si <c>DiarioAlIniciar</c>) y luego una vez por cada dia
    /// calendario de Colombia. Marca el dia al decidirlo (no al terminar), como el origen: una corrida larga no se repite.
    /// </summary>
    public bool DebeCorrerDiario()
    {
        var hoy = clsHoraColombia.FechaColombia(reloj.GetUtcNow());
        lock (_candadoDia)
        {
            if (!_diaInicializado)
            {
                _diaInicializado = true;
                if (!opciones.Value.DiarioAlIniciar)
                {
                    _ultimoDiaDiario = hoy;   // hoy cuenta como ya hecho: el diario espera al proximo dia
                    return false;
                }
            }
            if (_ultimoDiaDiario == hoy) return false;
            _ultimoDiaDiario = hoy;
            return true;
        }
    }

    /// <summary>
    /// Lanza lo que corresponde a este tick SIN esperarlo (el grupo frecuente siempre; el diario si toca) y devuelve las tareas
    /// para que quien llama pueda esperarlas al apagar. Las tareas nunca terminan en excepcion.
    /// </summary>
    public IReadOnlyList<Task> IniciarTick(CancellationToken ct)
    {
        var tareas = new List<Task> { EjecutarFrecuenteAsync(ct) };
        if (DebeCorrerDiario()) tareas.Add(EjecutarDiarioAsync(ct));
        return tareas;
    }

    /// <summary>Corre el grupo frecuente. Devuelve null si se omitio porque ya hay una corrida (en este proceso o en otra replica).</summary>
    public Task<IReadOnlyList<clsResultadoJob>?> EjecutarFrecuenteAsync(CancellationToken ct) =>
        EjecutarGrupoAsync(clsNombresJob.FRECUENTE, _frecuente, async token =>
        {
            var resultados = new List<clsResultadoJob>();
            // Telemetria y tanqueos son independientes; rutas necesita que los viajes nuevos ya esten en la base: cadena secuencial.
            var tareas = new[]
            {
                EjecutarCadenaAsync([clsNombresJob.TELEMETRIA], token),
                EjecutarCadenaAsync([clsNombresJob.TANQUEOS], token),
                EjecutarCadenaAsync([clsNombresJob.VIAJES, clsNombresJob.RUTAS_INCREMENTAL], token),
            };
            foreach (var r in await Task.WhenAll(tareas)) resultados.AddRange(r);
            return resultados;
        }, ct);

    /// <summary>Corre el grupo diario. Devuelve null si se omitio porque ya hay una corrida (en este proceso o en otra replica).</summary>
    public Task<IReadOnlyList<clsResultadoJob>?> EjecutarDiarioAsync(CancellationToken ct) =>
        EjecutarGrupoAsync(clsNombresJob.DIARIO, _diario, token => EjecutarCadenaAsync(clsNombresJob.OrdenDiario, token), ct);

    /// <summary>
    /// Ejecuta por nombre (modo manual <c>--job</c>): un job individual (sin bloqueos; es una corrida deliberada) o uno de los
    /// grupos (con sus bloqueos). Devuelve vacio si un grupo se omitio por estar en curso.
    /// </summary>
    public async Task<IReadOnlyList<clsResultadoJob>> EjecutarPorNombreAsync(string nombre, CancellationToken ct)
    {
        switch (nombre)
        {
            case clsNombresJob.FRECUENTE:
                return await EjecutarFrecuenteAsync(ct) ?? [];
            case clsNombresJob.DIARIO:
                return await EjecutarDiarioAsync(ct) ?? [];
            default:
                if (!clsNombresJob.Individuales.Contains(nombre, StringComparer.Ordinal))
                    throw new ArgumentException(
                        $"Job '{nombre}' desconocido. Validos: {string.Join(", ", clsNombresJob.Todos)}.", nameof(nombre));
                return [await EjecutarJobAsync(nombre, ct)];
        }
    }

    /// <summary>
    /// Codigo de salida del modo manual: 0 si todo salio bien, 1 si algo fallo. Un job individual solo es exitoso si termino Ok
    /// (omitido por falta de configuracion o con errores de registros cuentan como fallo, igual que los <c>npm run db:sync-*</c>
    /// que salian con 1 ante errores); en un grupo un job omitido por configuracion no lo hace fallar, pero si un fallo o errores.
    /// </summary>
    public static int CalcularCodigoSalida(string nombre, IReadOnlyList<clsResultadoJob> resultados)
    {
        var individual = clsNombresJob.Individuales.Contains(nombre, StringComparer.Ordinal);
        var fallo = resultados.Any(r => individual
            ? r.Estado != EstadoJob.Ok
            : r.Estado is EstadoJob.Fallido or EstadoJob.ConErrores or EstadoJob.Cancelado);
        return fallo ? 1 : 0;
    }

    /// <summary>Corre un job aislado: nunca lanza (salvo que se lo pida cancelar a quien llama), siempre devuelve su resultado.</summary>
    public async Task<clsResultadoJob> EjecutarJobAsync(string nombre, CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        log.LogInformation("sync {Job}: iniciando", nombre);
        try
        {
            var resultado = await ejecutor.EjecutarAsync(nombre, ct);
            cronometro.Stop();
            var estado = resultado.TieneErrores ? EstadoJob.ConErrores : EstadoJob.Ok;
            if (resultado.TieneErrores)
                log.LogWarning("sync {Job}: terminado con errores en {Duracion:0.0}s ({Resumen}). Primeros: {Detalle}",
                    nombre, cronometro.Elapsed.TotalSeconds, resultado.Resumen(),
                    string.Join(" | ", resultado.Errores.Take(3).Select(e => $"{e.Clave}: {e.Mensaje}")));
            else
                log.LogInformation("sync {Job}: ok en {Duracion:0.0}s ({Resumen})", nombre, cronometro.Elapsed.TotalSeconds, resultado.Resumen());
            return new clsResultadoJob(nombre, estado, cronometro.Elapsed, resultado, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            log.LogWarning("sync {Job}: cancelado por el apagado", nombre);
            return new clsResultadoJob(nombre, EstadoJob.Cancelado, cronometro.Elapsed, null, "Cancelado");
        }
        catch (clsServiceUnavailableException ex)
        {
            // Falta configuracion (FPSAP/EBS/GEOTAB) o el servicio externo no responde: aviso, no error fatal.
            log.LogWarning("sync {Job}: omitido [{Codigo}] {Mensaje}", nombre, ex.Code, ex.Message);
            return new clsResultadoJob(nombre, EstadoJob.Omitido, cronometro.Elapsed, null, $"{ex.Code}: {ex.Message}");
        }
        catch (Exception ex)
        {
            log.LogError(ex, "sync {Job}: fallo tras {Duracion:0.0}s", nombre, cronometro.Elapsed.TotalSeconds);
            return new clsResultadoJob(nombre, EstadoJob.Fallido, cronometro.Elapsed, null, ex.Message);
        }
    }

    private async Task<IReadOnlyList<clsResultadoJob>> EjecutarCadenaAsync(IReadOnlyList<string> jobs, CancellationToken ct)
    {
        var resultados = new List<clsResultadoJob>(jobs.Count);
        foreach (var job in jobs)
        {
            if (ct.IsCancellationRequested) break;
            resultados.Add(await EjecutarJobAsync(job, ct));
        }
        return resultados;
    }

    /// <summary>Protocolo comun de un grupo: semaforo del proceso, bloqueo entre replicas, cuerpo y liberacion.</summary>
    private async Task<IReadOnlyList<clsResultadoJob>?> EjecutarGrupoAsync(
        string grupo, SemaphoreSlim local, Func<CancellationToken, Task<IReadOnlyList<clsResultadoJob>>> cuerpo, CancellationToken ct)
    {
        if (!await local.WaitAsync(0, CancellationToken.None))
        {
            log.LogInformation("sync {Grupo}: corrida anterior en curso; se omite", grupo);
            return null;
        }

        try
        {
            IAsyncDisposable? candado;
            try
            {
                candado = await bloqueo.IntentarAdquirirAsync(PREFIJO_BLOQUEO + grupo, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                // Sin base propia no hay bloqueo posible; se reintenta en el proximo tick.
                log.LogError(ex, "sync {Grupo}: no se pudo adquirir el bloqueo distribuido; se omite", grupo);
                return null;
            }

            if (candado is null)
            {
                log.LogInformation("sync {Grupo}: otra replica tiene el bloqueo; se omite", grupo);
                return null;
            }

            await using (candado)
            {
                var cronometro = Stopwatch.StartNew();
                var resultados = await cuerpo(ct);
                log.LogInformation("sync {Grupo}: terminado en {Duracion:0.0}s ({Ok} ok, {ConErrores} con errores, {Omitidos} omitidos, {Fallidos} fallidos)",
                    grupo, cronometro.Elapsed.TotalSeconds,
                    resultados.Count(r => r.Estado == EstadoJob.Ok), resultados.Count(r => r.Estado == EstadoJob.ConErrores),
                    resultados.Count(r => r.Estado == EstadoJob.Omitido), resultados.Count(r => r.Estado == EstadoJob.Fallido));
                return resultados;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // El cuerpo ya aisla cada job; esto es un seguro para que la tarea del tick jamas termine en excepcion.
            log.LogError(ex, "sync {Grupo}: error inesperado", grupo);
            return null;
        }
        finally
        {
            local.Release();
        }
    }
}
