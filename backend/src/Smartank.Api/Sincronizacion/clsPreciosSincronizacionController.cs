using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Ports;

namespace Smartank.Api.Sincronizacion;

/// <summary>Actualizacion de precios de las estaciones (EDS): automatica desde las fuentes de cada red o manual como respaldo.</summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/estaciones/precios/sync")]
public class clsPreciosSincronizacionController(
    ICommandHandler<clsSincronizarPreciosCommand, clsResultadoPreciosDto> sincronizar,
    ICommandHandler<clsActualizarPreciosManualCommand, clsResultadoPreciosManualDto> actualizarManual,
    ICurrentUserPort usuarioActual)
    : ControllerBase
{
    /// <summary>
    /// Actualiza los precios. Con <c>precios</c> en el cuerpo hace la carga manual (<c>{ modo: "manual", actualizadas, estaciones }</c>);
    /// sin cuerpo consulta las fuentes reales de Terpel y Distracom (<c>{ modo: "automatico", fuente: { distracom, terpel }, errores }</c>).
    /// Solo el rol Admin. Deja constancia en la bitacora de auditoria.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(clsResultadoPreciosDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(clsResultadoPreciosManualDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Sincronizar(
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] clsSincronizarPreciosRequest? req,
        CancellationToken ct = default)
    {
        if (req?.Precios is { Count: > 0 } precios)
        {
            var manual = await actualizarManual.Handle(
                new clsActualizarPreciosManualCommand(
                    usuarioActual.UsuarioId, precios.Select(p => new clsPrecioManualItemDto(p.EstacionId, p.PrecioGalonActual)).ToList()),
                ct);
            return Ok(manual);
        }

        return Ok(await sincronizar.Handle(new clsSincronizarPreciosCommand(usuarioActual.UsuarioId, Auditar: true), ct));
    }
}
