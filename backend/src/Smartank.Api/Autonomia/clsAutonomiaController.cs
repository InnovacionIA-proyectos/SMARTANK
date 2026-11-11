using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Smartank.Application.CasosDeUso.Autonomia;
using Smartank.Application.CasosDeUso.Autonomia.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Autonomia;

/// <summary>Autonomia de un viaje: cuantos kilometros alcanza el vehiculo con el combustible que le queda.</summary>
[ApiController]
[Authorize(Roles = "Despacho,Admin")]
[Route("api/viajes/{id:guid}/autonomia")]
public class clsAutonomiaController(ICommandHandler<clsCalcularAutonomiaCommand, clsAutonomiaDto> calcular) : ControllerBase
{
    /// <summary>Calcula la autonomia con la ultima lectura de nivel del vehiculo y la guarda en el viaje.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(clsAutonomiaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsAutonomiaDto>> Calcular(Guid id, CancellationToken ct = default)
        => Ok(await calcular.Handle(new clsCalcularAutonomiaCommand(id), ct));
}
