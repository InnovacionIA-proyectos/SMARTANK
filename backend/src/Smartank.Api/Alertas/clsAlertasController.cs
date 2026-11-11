using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.Auth;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Alertas;
using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Alertas;

/// <summary>Bandeja de alertas de control. Solo Control de Flota y Admin la ven y la gestionan; los servicios las crean con el secreto de servicio.</summary>
[ApiController]
[Authorize(Roles = clsRoles.CONTROL_FLOTA_ADMIN)]
[Route("api/alertas")]
public class clsAlertasController(
    IQueryHandler<clsListarAlertasQuery, clsAlertasResponse> listar,
    ICommandHandler<clsGestionarAlertaCommand, clsAlertaDto> gestionar,
    ICommandHandler<clsCrearAlertaCommand, clsAlertaDto> crear)
    : ControllerBase
{
    /// <summary>Lista las alertas con la placa del vehiculo, de la mas reciente a la mas antigua; <c>estado</c> filtra por estado.</summary>
    [HttpGet]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsAlertasResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<clsAlertasResponse>> Listar([FromQuery] string? estado, CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarAlertasQuery(estado), ct));

    /// <summary>Cambia el estado de gestion de una alerta (nueva -> en_revision -> gestionada o descartada).</summary>
    /// <remarks>409 TRANSICION_ALERTA_INVALIDA si el cambio no esta permitido; 409 CONFLICTO_CONCURRENCIA si otro usuario la modifico a la vez.</remarks>
    [HttpPatch("{id}")]
    [ProducesResponseType(typeof(clsAlertaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsAlertaDto>> Gestionar(Guid id, [FromBody] clsGestionarAlertaRequest req, CancellationToken ct = default)
        => Ok(await gestionar.Handle(new clsGestionarAlertaCommand(id, req.Estado), ct));

    /// <summary>Crea una alerta en estado nueva. Sin sesion: requiere el header X-Service-Secret.</summary>
    [HttpPost]
    [AllowAnonymous]
    [clsRequiereSecretoDeServicio]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsAlertaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<clsAlertaDto>> Crear([FromBody] clsCrearAlertaRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(new clsCrearAlertaCommand(req.Tipo, req.VehiculoId, req.EventoId, req.MagnitudDesviacion), ct);
        return StatusCode(StatusCodes.Status201Created, dto);
    }
}

/// <summary>Cuerpo de PATCH /api/alertas/{id}.</summary>
public sealed record clsGestionarAlertaRequest(string? Estado);

/// <summary>Cuerpo de POST /api/alertas.</summary>
public sealed record clsCrearAlertaRequest(string? Tipo, Guid? VehiculoId, Guid? EventoId, double? MagnitudDesviacion);
