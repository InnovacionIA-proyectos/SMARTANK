using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Bitacora;
using Smartank.Application.CasosDeUso.Bitacora.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Bitacora;

/// <summary>Bitacora del conductor: la orden de tanqueo a la que lo lleva el enlace de WhatsApp (sin sesion, con token firmado).</summary>
[ApiController]
[AllowAnonymous]
[Route("api/bitacora")]
public class clsBitacoraController(IQueryHandler<clsObtenerBitacoraQuery, clsBitacoraDto> obtener) : ControllerBase
{
    /// <summary>Datos de la orden (placa, manifiesto, ruta, estacion, galones, estado) y si la bitacora ya fue registrada.</summary>
    [HttpGet("{token}")]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaEscrituraPublica)]
    [ProducesResponseType(typeof(clsBitacoraDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<clsBitacoraDto>> Obtener(string token, CancellationToken ct = default)
        => Ok(await obtener.Handle(new clsObtenerBitacoraQuery(token), ct));
}
