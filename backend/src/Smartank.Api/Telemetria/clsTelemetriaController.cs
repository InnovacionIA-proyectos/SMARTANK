using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.Auth;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Telemetria;
using Smartank.Application.CasosDeUso.Telemetria.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Telemetria;

/// <summary>Ingesta de lecturas de telemetria del proveedor (sin sesion de usuario: se autentica con el secreto de servicio).</summary>
[ApiController]
[Route("api/telemetria")]
public class clsTelemetriaController(ICommandHandler<clsIngestarTelemetriaCommand, clsIngestaTelemetriaResponse> ingestar)
    : ControllerBase
{
    /// <summary>Registra el nivel de tanque, el odometro y la posicion de un vehiculo, identificado por placa.</summary>
    /// <remarks>Requiere el header X-Service-Secret. Los datos opcionales que no vienen conservan su valor anterior.</remarks>
    [HttpPost("ingesta")]
    [AllowAnonymous]
    [clsRequiereSecretoDeServicio]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsIngestaTelemetriaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<clsIngestaTelemetriaResponse>> Ingestar([FromBody] clsIngestaTelemetriaRequest req, CancellationToken ct = default)
        => Ok(await ingestar.Handle(
            new clsIngestarTelemetriaCommand(req.Placa, req.NivelGalones, req.Odometro, req.Lat, req.Lng, req.LeidoAt), ct));
}

/// <summary>Cuerpo de POST /api/telemetria/ingesta. Solo placa y nivel_galones son obligatorios.</summary>
public sealed record clsIngestaTelemetriaRequest(
    string? Placa,
    decimal? NivelGalones,
    double? Odometro,
    double? Lat,
    double? Lng,
    DateTimeOffset? LeidoAt);
