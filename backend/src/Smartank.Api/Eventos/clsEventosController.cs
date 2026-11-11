using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Eventos;
using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Eventos;

/// <summary>Eventos de tanqueo: historial combinado, bitacora del conductor y verificacion de la carga.</summary>
[ApiController]
[Authorize]
[Route("api/eventos")]
public class clsEventosController(
    IQueryHandler<clsListarHistorialTanqueosQuery, clsHistorialTanqueosDto> historial,
    ICommandHandler<clsRegistrarEventoCommand, clsEventoDto> registrar,
    ICommandHandler<clsVerificarEventoCommand, clsVerificacionDto> verificar)
    : ControllerBase
{
    /// <summary>
    /// Historial de tanqueos combinado (Terpel/Distracom mas la autorizacion y bitacora de la app cuando se pueden casar).
    /// Filtros: placa (parcial), proveedor (terpel | distracom), desde y hasta (yyyy-MM-dd, inclusivos). Requiere sesion.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(clsHistorialTanqueosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsHistorialTanqueosDto>> Historial(
        [FromQuery] string? placa, [FromQuery] string? proveedor, [FromQuery] string? desde, [FromQuery] string? hasta,
        CancellationToken ct = default)
        => Ok(await historial.Handle(new clsListarHistorialTanqueosQuery(placa, proveedor, desde, hasta), ct));

    /// <summary>
    /// El conductor reporta su carga con el token de su enlace (sin sesion). La autorizacion debe estar autorizada y vigente,
    /// y admite un solo reporte. Tras guardarlo se verifica la carga automaticamente.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaEscrituraPublica)]
    [ProducesResponseType(typeof(clsEventoDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<clsEventoDto>> Registrar([FromBody] clsRegistrarEventoRequest req, CancellationToken ct = default)
    {
        var dto = await registrar.Handle(
            new clsRegistrarEventoCommand(
                req.Token, req.GalonesCargados, req.PrecioPagado, req.FotoSurtidorUrl,
                req.OdometroEvento, req.NivelAntes, req.NivelDespues),
            ct);
        return Created($"/api/eventos/{dto.Id}", dto);
    }

    /// <summary>
    /// Verifica una carga: cruza nivel antes y despues contra los galones y crea las alertas que correspondan. Normalmente se
    /// ejecuta sola al registrar el evento; este endpoint permite repetirla si fallo (una autorizacion ya verificada responde 409).
    /// </summary>
    [HttpPost("{id:guid}/verificar")]
    [Authorize(Roles = "Despacho,ControlFlota,Admin")]
    [ProducesResponseType(typeof(clsVerificacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsVerificacionDto>> Verificar(Guid id, CancellationToken ct = default)
        => Ok(await verificar.Handle(new clsVerificarEventoCommand(id), ct));
}
