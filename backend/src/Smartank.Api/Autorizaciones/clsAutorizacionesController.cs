using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Autorizaciones;

/// <summary>Autorizaciones de tanqueo: crear la propuesta, autorizarla, pre-autorizar en la EDS y notificar al conductor.</summary>
[ApiController]
[Authorize]
[Route("api/autorizaciones")]
public class clsAutorizacionesController(
    ICommandHandler<clsCrearAutorizacionCommand, clsAutorizacionDto> crear,
    ICommandHandler<clsAutorizarAutorizacionCommand, clsAutorizacionDto> autorizar,
    ICommandHandler<clsPreautorizarEdsCommand, clsPreautorizacionEdsDto> preautorizar,
    ICommandHandler<clsNotificarAutorizacionCommand, clsNotificarDto> notificar,
    IQueryHandler<clsObtenerAutorizacionDetalleQuery, clsAutorizacionDetalleDto> detalle,
    IQueryHandler<clsListarAutorizacionesQuery, clsAutorizacionesListaDto> listar)
    : ControllerBase
{
    private const string ROLES_ESCRITURA = "Despacho,Admin";

    /// <summary>Lista autorizaciones (mas recientes primero, maximo 200), con filtros opcionales por estado y viaje.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(clsAutorizacionesListaDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<clsAutorizacionesListaDto>> Listar(
        [FromQuery] string? estado, [FromQuery(Name = "viaje_id")] Guid? viajeId, CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarAutorizacionesQuery(estado, viajeId), ct));

    /// <summary>Detalle de una autorizacion con estacion, placa, viaje, conductor, telemetria y ruta.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(clsAutorizacionDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsAutorizacionDetalleDto>> Obtener(Guid id, CancellationToken ct = default)
        => Ok(await detalle.Handle(new clsObtenerAutorizacionDetalleQuery(id), ct));

    /// <summary>Crea una autorizacion en estado propuesta con galones maximos, precio techo y ventana.</summary>
    [HttpPost]
    [Authorize(Roles = ROLES_ESCRITURA)]
    [ProducesResponseType(typeof(clsAutorizacionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsAutorizacionDto>> Crear([FromBody] clsCrearAutorizacionRequest req, CancellationToken ct = default)
    {
        var dto = await crear.Handle(
            new clsCrearAutorizacionCommand(
                req.ViajeId, req.VehiculoId, req.EstacionId, req.GalonesMax, req.PrecioTecho,
                req.DeltaAhorroEstimado, req.FechaInicio, req.FechaFin),
            ct);
        return Created($"/api/autorizaciones/{dto.Id}", dto);
    }

    /// <summary>Despacho confirma la propuesta (propuesta a autorizada).</summary>
    [HttpPost("{id:guid}/autorizar")]
    [Authorize(Roles = ROLES_ESCRITURA)]
    [ProducesResponseType(typeof(clsAutorizacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsAutorizacionDto>> Autorizar(Guid id, CancellationToken ct = default)
        => Ok(await autorizar.Handle(new clsAutorizarAutorizacionCommand(id), ct));

    /// <summary>Envia a la EDS la pre-autorizacion (placa, galones, ventana) de una autorizacion autorizada.</summary>
    [HttpPost("{id:guid}/preautorizar-eds")]
    [Authorize(Roles = ROLES_ESCRITURA)]
    [ProducesResponseType(typeof(clsPreautorizacionEdsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<clsPreautorizacionEdsDto>> PreautorizarEds(Guid id, CancellationToken ct = default)
        => Ok(await preautorizar.Handle(new clsPreautorizarEdsCommand(id), ct));

    /// <summary>
    /// Envia la orden de tanqueo al conductor por WhatsApp con el enlace de su bitacora. Si WhatsApp no esta configurado o falla,
    /// responde 200 con <c>whatsapp.enviado = false</c> y el motivo.
    /// </summary>
    [HttpPost("{id:guid}/notificar")]
    [Authorize(Roles = ROLES_ESCRITURA)]
    [ProducesResponseType(typeof(clsNotificarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<clsNotificarDto>> Notificar(Guid id, CancellationToken ct = default)
        => Ok(await notificar.Handle(new clsNotificarAutorizacionCommand(id), ct));
}
