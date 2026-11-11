using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

namespace Smartank.Worker;

/// <summary>
/// Servicio en segundo plano que corre las sincronizaciones: en cada tick de <c>PeriodicTimer</c> (cada
/// <c>Sync__IntervaloSegundos</c>) lanza el grupo frecuente y, si toca, el diario. Replica scheduler.mjs del origen; la logica
/// de la agenda (no solapar, una vez por dia, aislamiento de jobs, bloqueo entre replicas) vive en
/// <see cref="clsOrquestadorSincronizacion"/>. Los grupos NO se esperan entre ticks: uno largo no retrasa el siguiente tick, que lo
/// omite si el anterior sigue en curso.
/// </summary>
public sealed class clsSincronizacionWorker(
    clsOrquestadorSincronizacion orquestador,
    IOptions<clsSyncOptions> opciones,
    ILogger<clsSincronizacionWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken detener)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Max(1, opciones.Value.IntervaloSegundos));
        var enCurso = new ConcurrentDictionary<Task, byte>();

        void Lanzar()
        {
            foreach (var tarea in orquestador.IniciarTick(detener))
            {
                enCurso[tarea] = 0;
                _ = tarea.ContinueWith(t => enCurso.TryRemove(t, out _), TaskScheduler.Default);
            }
        }

        log.LogInformation(
            "Worker de sincronizacion iniciado: grupo frecuente (telemetria, tanqueos, viajes y rutas incremental) cada {Intervalo}s; grupo diario una vez por dia de Colombia (al iniciar: {AlIniciar})",
            (int)intervalo.TotalSeconds, opciones.Value.DiarioAlIniciar);

        try
        {
            Lanzar();
            using var temporizador = new PeriodicTimer(intervalo);
            while (await temporizador.WaitForNextTickAsync(detener)) Lanzar();
        }
        catch (OperationCanceledException)
        {
            // Apagado solicitado: se sale del ciclo y se espera a lo que este corriendo.
        }
        finally
        {
            var pendientes = enCurso.Keys.ToArray();
            if (pendientes.Length > 0)
            {
                log.LogInformation("Apagando: esperando a {Cantidad} grupo(s) en curso (ya recibieron la cancelacion)", pendientes.Length);
                try { await Task.WhenAll(pendientes); }
                catch (Exception ex) { log.LogWarning(ex, "Un grupo termino con error durante el apagado"); }
            }
            log.LogInformation("Worker de sincronizacion detenido");
        }
    }
}
