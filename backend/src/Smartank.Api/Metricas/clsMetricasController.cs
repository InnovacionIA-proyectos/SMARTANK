using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.Auth;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Metricas;
using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Metricas;

/// <summary>Metricas de recuperacion de margen del tablero. Solo Finanzas y Admin.</summary>
[ApiController]
[Authorize(Roles = clsRoles.FINANZAS_ADMIN)]
[Route("api/metricas")]
public class clsMetricasController(IQueryHandler<clsObtenerMetricasRecuperacionQuery, clsMetricasRecuperacionDto> recuperacion)
    : ControllerBase
{
    /// <summary>KPIs de recuperacion de margen sobre el historial real de tanqueos de ACPM, con filtros opcionales.</summary>
    /// <param name="programa">Programa de flota del vehiculo.</param>
    /// <param name="proveedor">Red del historial (terpel o distracom).</param>
    /// <param name="producto">Producto tanqueado.</param>
    /// <param name="anio">Anio del tanqueo.</param>
    /// <param name="mes">Mes (1-12) del tanqueo.</param>
    /// <param name="ruta">Ruta del viaje al que se liga el tanqueo.</param>
    /// <param name="ct">Cancelacion de la peticion.</param>
    [HttpGet("recuperacion")]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsMetricasRecuperacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<clsMetricasRecuperacionDto>> Recuperacion(
        [FromQuery] string? programa,
        [FromQuery] string? proveedor,
        [FromQuery] string? producto,
        [FromQuery] string? anio,
        [FromQuery] string? mes,
        [FromQuery] string? ruta,
        CancellationToken ct = default)
        => Ok(await recuperacion.Handle(
            new clsObtenerMetricasRecuperacionQuery(programa, proveedor, producto, anio, mes, ruta), ct));
}
