using Microsoft.EntityFrameworkCore;
using Smartank.Api.Common.Errors;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Api.Common.Middleware;

/// <summary>Traduce las excepciones de Application al codigo HTTP que corresponde y captura las no controladas. Responde siempre JSON { code, message, timestamp }.</summary>
public class clsErrorHandlingMiddleware(RequestDelegate next, ILogger<clsErrorHandlingMiddleware> logger, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (clsNotFoundException ex)
        {
            await Controlada(context, ex, StatusCodes.Status404NotFound);
        }
        catch (clsValidationFailedException ex)
        {
            await Controlada(context, ex, StatusCodes.Status400BadRequest);
        }
        catch (clsConflictException ex)
        {
            await Controlada(context, ex, StatusCodes.Status409Conflict);
        }
        catch (clsForbiddenException ex)
        {
            await Controlada(context, ex, StatusCodes.Status403Forbidden);
        }
        catch (clsUnauthorizedException ex)
        {
            await Controlada(context, ex, StatusCodes.Status401Unauthorized);
        }
        catch (clsLimiteExcedidoException ex)
        {
            await Controlada(context, ex, StatusCodes.Status429TooManyRequests);
        }
        catch (clsServiceUnavailableException ex)
        {
            await Controlada(context, ex, StatusCodes.Status503ServiceUnavailable);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Concurrencia optimista (rowversion): otro usuario modifico el registro primero.
            logger.LogWarning(ex, "Conflicto de concurrencia en {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status409Conflict, "CONFLICTO_CONCURRENCIA",
                "El registro fue modificado por otro usuario. Recargue y vuelva a intentar.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Excepcion no controlada en {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteAsync(
                context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                env.IsDevelopment() ? ex.Message : "Ocurrio un error inesperado.");
        }
    }

    private async Task Controlada(HttpContext context, clsAppException ex, int status)
    {
        logger.LogWarning(ex, "{Code} en {Method} {Path}", ex.Code, context.Request.Method, context.Request.Path);
        await WriteAsync(context, status, ex.Code, ex.Message, ex.Details);
    }

    public static async Task WriteAsync(HttpContext context, int statusCode, string code, string message, object? details = null)
    {
        if (context.Response.HasStarted) return;

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new clsApiError(code, message, DateTimeOffset.UtcNow, details));
    }
}
