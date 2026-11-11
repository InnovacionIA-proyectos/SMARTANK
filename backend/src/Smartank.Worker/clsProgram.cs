using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;
using Smartank.Infrastructure.Persistence;
using Smartank.Infrastructure.Sincronizacion;
using Smartank.Worker;

// Host de procesos en segundo plano de SMARTANK (segundo punto de entrada de la solucion).
//   dotnet Smartank.Worker.dll                  -> servicio: grupos frecuente (cada Sync__IntervaloSegundos) y diario.
//   dotnet Smartank.Worker.dll --job <nombre>   -> ejecuta UNA vez y sale con 0 (ok) o 1 (fallo).
//      nombres: vehiculos, conductores, viajes, rutas, rutas-incremental, tanqueos, telemetria, precios, precios-ruta,
//               rendimiento, frecuente, diario.

var job = clsArgumentosWorker.ObtenerJob(args, out var errorArgumentos);
if (errorArgumentos is not null)
{
    Console.Error.WriteLine($"SMARTANK Worker: {errorArgumentos}");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);

// Una linea por evento, en la consola (el contenedor recoge stdout).
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    o.UseUtcTimestamp = true;
});

// La base propia es lo unico obligatorio: sin ella no hay donde escribir ni bloqueo entre replicas. Fpsap, Ebs y Geotab
// son opcionales: cada job afectado se omite con un aviso, no es un error fatal.
if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Smartank")))
{
    Console.Error.WriteLine(
        "SMARTANK Worker: falta la cadena de conexion de la base propia. Defina la variable ConnectionStrings__Smartank.");
    return 1;
}

builder.Services
    .AddPersistence(builder.Configuration)
    .AddSincronizacionUseCases()
    .AddSincronizacionInfrastructure(builder.Configuration);

if (job is null) builder.Services.AddHostedService<clsSincronizacionWorker>();

using var host = builder.Build();

if (job is null)
{
    await host.RunAsync();
    return 0;
}

// Modo manual: una sola corrida deliberada; Ctrl+C la cancela.
using var cancelacion = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancelacion.Cancel();
};

var orquestador = host.Services.GetRequiredService<clsOrquestadorSincronizacion>();
var log = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Smartank.Worker");
var resultados = await orquestador.EjecutarPorNombreAsync(job, cancelacion.Token);
if (resultados.Count == 0) log.LogWarning("Job {Job}: no se ejecuto porque ya hay una corrida en curso (este proceso u otra replica)", job);

var codigo = clsOrquestadorSincronizacion.CalcularCodigoSalida(job, resultados);
log.LogInformation("Job {Job}: terminado con codigo de salida {Codigo}", job, codigo);
return codigo;
