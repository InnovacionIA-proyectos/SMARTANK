using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.Middleware;

namespace Smartank.Api.Common.RateLimiting;

/// <summary>
/// Rate limiting con el middleware nativo de ASP.NET Core. Tres politicas, particionadas por IP:
/// <see cref="PoliticaListadosEstandar"/> (lecturas normales), <see cref="PoliticaEscrituraPublica"/> (endpoints sin sesion
/// como la bitacora del conductor) y <see cref="PoliticaLogin"/> (intentos de inicio de sesion, mas estricta).
/// </summary>
public static class clsRateLimitingExtensions
{
    public const string PoliticaListadosEstandar = "listados-estandar";
    public const string PoliticaEscrituraPublica = "escritura-publica";
    public const string PoliticaLogin = "login";

    public static IServiceCollection AddRateLimitingSmartank(this IServiceCollection services) =>
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Mismo shape de error que el resto de la API: el rate limiter corre despues de clsErrorHandlingMiddleware.
            o.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.Headers.RetryAfter = "30";
                await clsErrorHandlingMiddleware.WriteAsync(
                    context.HttpContext, StatusCodes.Status429TooManyRequests,
                    "DEMASIADAS_SOLICITUDES",
                    "Ha alcanzado el limite de solicitudes para este recurso. Intente de nuevo en unos segundos.");
            };

            o.AddPolicy(PoliticaListadosEstandar, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(Clave(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            o.AddPolicy(PoliticaEscrituraPublica, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(Clave(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));

            o.AddPolicy(PoliticaLogin, ctx =>
                RateLimitPartition.GetFixedWindowLimiter(Clave(ctx), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

    private static string Clave(HttpContext ctx) => ctx.Connection.RemoteIpAddress?.ToString() ?? "anonimo";
}
