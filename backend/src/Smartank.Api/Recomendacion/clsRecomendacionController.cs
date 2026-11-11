using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Smartank.Application.CasosDeUso.Recomendacion;
using Smartank.Application.CasosDeUso.Recomendacion.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Recomendacion;

/// <summary>Recomendacion de tanqueo: EDS mas barata dentro de la autonomia, con galones y precio techo.</summary>
[ApiController]
[Authorize(Roles = "Despacho,Admin")]
[Route("api/viajes/{id:guid}/recomendacion")]
public class clsRecomendacionController(ICommandHandler<clsRecomendarTanqueoCommand, clsRecomendacionDto> recomendar) : ControllerBase
{
    /// <summary>
    /// Recomienda donde tanquear y deja la autorizacion en estado propuesta; si el viaje ya tiene una propuesta, la actualiza.
    /// El cuerpo es opcional.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(clsRecomendacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsRecomendacionDto>> Recomendar(
        Guid id,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] clsRecomendarTanqueoRequest? req = null,
        CancellationToken ct = default)
        => Ok(await recomendar.Handle(new clsRecomendarTanqueoCommand(id, req?.Galones), ct));
}
