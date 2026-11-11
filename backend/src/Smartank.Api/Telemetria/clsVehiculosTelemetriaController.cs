using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Telemetria;
using Smartank.Application.CasosDeUso.Telemetria.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Telemetria;

/// <summary>Ultimo estado de tanque de un vehiculo, para cualquier usuario con sesion.</summary>
[ApiController]
[Authorize]
[Route("api/vehiculos")]
public class clsVehiculosTelemetriaController(IQueryHandler<clsObtenerTelemetriaVehiculoQuery, clsTelemetriaVehiculoDto> obtener)
    : ControllerBase
{
    /// <summary>Nivel de tanque y odometro mas recientes del vehiculo; <c>tiene_telemetria</c> es false si nunca ha reportado nivel.</summary>
    [HttpGet("{id}/telemetria")]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsTelemetriaVehiculoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsTelemetriaVehiculoDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await obtener.Handle(new clsObtenerTelemetriaVehiculoQuery(id), ct));
}
