using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Smartank.Api.Common.Auth;
using Smartank.Api.Common.Errors;
using Smartank.Api.Common.Middleware;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.CasosDeUso.Alertas;
using Smartank.Application.CasosDeUso.Auth;
using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.CasosDeUso.Autonomia;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Bitacora;
using Smartank.Application.CasosDeUso.Estaciones;
using Smartank.Application.CasosDeUso.Eventos;
using Smartank.Application.CasosDeUso.Metricas;
using Smartank.Application.CasosDeUso.Recomendacion;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Telemetria;
using Smartank.Application.CasosDeUso.Uploads;
using Smartank.Application.CasosDeUso.Viajes;
using Smartank.Infrastructure.Alertas;
using Smartank.Infrastructure.Auth;
using Smartank.Infrastructure.Autorizaciones;
using Smartank.Infrastructure.Autorizaciones.Adaptadores;
using Smartank.Infrastructure.Bitacora;
using Smartank.Infrastructure.Bitacora.Adaptadores;
using Smartank.Infrastructure.Estaciones;
using Smartank.Infrastructure.Eventos;
using Smartank.Infrastructure.Metricas;
using Smartank.Infrastructure.Persistence;
using Smartank.Infrastructure.Recomendacion;
using Smartank.Infrastructure.Sincronizacion;
using Smartank.Infrastructure.Telemetria;
using Smartank.Infrastructure.Uploads;
using Smartank.Infrastructure.Viajes;

// Composition root del API. Los casos de uso viven en Application, los adaptadores en Infrastructure; aqui solo se cablean.
// Comando de consola:  dotnet Smartank.Api.dll crear-admin   (crea el administrador inicial; ver DOKPLOY.md).

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(o => o.AddServerHeader = false);

// Configuracion local opcional (no se versiona: ver .gitignore).
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.local.json", optional: true, reloadOnChange: true);

builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});

var esProduccion = builder.Environment.IsProduction();

// --------------------------------------------------------------- Validacion de arranque (fail-closed en Production)
// Un secreto o un origen mal configurado no debe degradarse en silencio: es mejor no arrancar.
if (esProduccion)
{
    builder.Configuration.ValidarConfiguracionAutenticacion();

    if (builder.Configuration.GetSection(clsBitacoraOptions.SECCION).Get<clsBitacoraOptions>() is not { SecretoValido: true })
        throw new InvalidOperationException(
            $"Bitacora__Secret no esta configurado o mide menos de {clsBitacoraOptions.LONGITUD_MINIMA_SECRETO} caracteres.");

    if (string.IsNullOrWhiteSpace(builder.Configuration["Servicio:Secreto"]))
        throw new InvalidOperationException("Servicio__Secreto no esta configurado en Production.");

    if (builder.Configuration["Cors:OrigenesPermitidos"] is null)
        throw new InvalidOperationException("Cors__OrigenesPermitidos no esta configurado en Production.");

    if (string.IsNullOrWhiteSpace(builder.Configuration["App:UrlPublica"]))
        throw new InvalidOperationException("App__UrlPublica no esta configurado en Production.");

    if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Smartank")))
        throw new InvalidOperationException("ConnectionStrings__Smartank no esta configurado en Production.");
}

// El modo de pruebas de WhatsApp desvia las ordenes a un numero fijo: prohibido en Production y exige numero en cualquier entorno.
(builder.Configuration.GetSection(clsWhatsappOptions.SECCION).Get<clsWhatsappOptions>() ?? new clsWhatsappOptions())
    .Validar(esProduccion);

// --------------------------------------------------------------- Persistencia + modulos
builder.Services.AddPersistence(builder.Configuration);

builder.Services.AddAuthUseCases();
builder.Services.AddTelemetriaUseCases();
builder.Services.AddEstacionesUseCases();
builder.Services.AddViajesUseCases();
builder.Services.AddAlertasUseCases();
builder.Services.AddMetricasUseCases();
builder.Services.AddAutonomiaUseCases();
builder.Services.AddRecomendacionUseCases();
builder.Services.AddAutorizacionesUseCases();
builder.Services.AddBitacoraUseCases();
builder.Services.AddEventosUseCases();
builder.Services.AddUploadsUseCases();
builder.Services.AddSincronizacionUseCases();

builder.Services.AddAuthInfrastructure(builder.Configuration);
builder.Services.AddTelemetriaInfrastructure(builder.Configuration);
builder.Services.AddEstacionesInfrastructure(builder.Configuration);
builder.Services.AddViajesInfrastructure(builder.Configuration);
builder.Services.AddAlertasInfrastructure(builder.Configuration);
builder.Services.AddMetricasInfrastructure(builder.Configuration);
builder.Services.AddAutorizacionesInfrastructure(builder.Configuration);
builder.Services.AddRecomendacionInfrastructure(builder.Configuration);
builder.Services.AddBitacoraInfrastructure(builder.Configuration);
builder.Services.AddEventosInfrastructure(builder.Configuration);
builder.Services.AddUploadsInfrastructure(builder.Configuration);
builder.Services.AddSincronizacionInfrastructure(builder.Configuration);

builder.Services.AddHttpContextAccessor();

// --------------------------------------------------------------- Autenticacion / Autorizacion
builder.Services.AddAutenticacionSmartank(builder.Configuration);

// --------------------------------------------------------------- Proxy, rate limiting, CORS
// El API corre detras de nginx (frontend) y Traefik: la IP y el esquema reales llegan en X-Forwarded-*. Solo se confia en
// ellos porque el API no se expone a internet (solo red interna).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddRateLimitingSmartank();

const string POLITICA_CORS = "clientes";
var origenesPermitidos = (builder.Configuration["Cors:OrigenesPermitidos"] ?? "http://localhost:4200")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy(POLITICA_CORS, p =>
{
    // La cookie de sesion exige origenes explicitos (no se puede combinar AllowCredentials con un comodin).
    p.WithOrigins(origenesPermitidos).AllowAnyHeader().AllowAnyMethod().AllowCredentials();
}));

// --------------------------------------------------------------- Controllers + JSON
// Contrato JSON: snake_case y enums como texto snake_case (igual que el sistema original y los CHECK de ddl.sql).
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        o.InvalidModelStateResponseFactory = ctx =>
        {
            var errores = ctx.ModelState
                .Where(kv => kv.Value!.Errors.Count > 0)
                .ToDictionary(kv => kv.Key, kv => kv.Value!.Errors.Select(e => e.ErrorMessage).ToArray());
            var error = new clsApiError("VALIDATION_ERROR", "Una o mas validaciones fallaron.", DateTimeOffset.UtcNow, errores);
            return new BadRequestObjectResult(error);
        };
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    // Lleva los /// <summary> de los controllers al contrato de /swagger (requiere GenerateDocumentationFile en Smartank.Api.csproj).
    var xml = Path.Combine(AppContext.BaseDirectory, $"{typeof(clsProgram).Assembly.GetName().Name}.xml");
    if (File.Exists(xml)) o.IncludeXmlComments(xml);
});

builder.Services.AddHealthChecks().AddDbContextCheck<clsSmartankDbContext>("database");

var app = builder.Build();

// --------------------------------------------------------------- Comando de consola: crear-admin
if (args.Contains("crear-admin"))
{
    using var scope = app.Services.CreateScope();
    try
    {
        var handler = scope.ServiceProvider
            .GetRequiredService<ICommandHandler<clsCrearAdministradorInicialCommand, clsCrearAdministradorResultado>>();
        var resultado = await handler.Handle(new clsCrearAdministradorInicialCommand());
        Console.WriteLine(resultado.Creado
            ? $"Administrador creado: {resultado.Email}"
            : $"El administrador {resultado.Email} ya existia: no se cambio nada.");
        return 0;
    }
    catch (Exception ex)
    {
        // Nunca se imprime la contrasena: solo el motivo.
        Console.Error.WriteLine($"No se pudo crear el administrador: {ex.Message}");
        return 1;
    }
}

// --------------------------------------------------------------- Pipeline
app.UseForwardedHeaders();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Sin UseHttpsRedirection: el TLS termina en Traefik y el trafico interno (nginx -> API) es HTTP por la red de Docker.
app.UseMiddleware<clsErrorHandlingMiddleware>();
app.UseCors(POLITICA_CORS);
app.UseAuthentication();
app.UseAuthorization();
// Despues de Authorization y antes de MapControllers: el 429 se decide sin llegar al handler.
app.UseRateLimiter();

app.MapControllers();

// /health responde JSON con el detalle por chequeo: un 503 en texto plano no dice que esta caido. Es publico.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (contexto, reporte) =>
    {
        contexto.Response.ContentType = "application/json; charset=utf-8";
        await contexto.Response.WriteAsJsonAsync(new
        {
            status = reporte.Status.ToString(),
            duracion = reporte.TotalDuration.TotalMilliseconds,
            chequeos = reporte.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    descripcion = e.Value.Description,
                    error = esProduccion ? null : e.Value.Exception?.Message,
                }),
        });
    },
}).AllowAnonymous();

app.Run();
return 0;

/// <summary>Marcador publico para que WebApplicationFactory&lt;clsProgram&gt; (tests de integracion) pueda referenciar el entry point.</summary>
public partial class clsProgram { }
