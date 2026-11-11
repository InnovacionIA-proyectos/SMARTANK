using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Estaciones;
using Smartank.Application.CasosDeUso.Estaciones.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Estaciones;

/// <summary>Consulta del catalogo de estaciones de servicio (EDS) con ubicacion y precio, para cualquier usuario con sesion.</summary>
[ApiController]
[Authorize]
[Route("api/estaciones")]
public class clsEstacionesConsultaController(IQueryHandler<clsListarEstacionesQuery, clsEstacionesResponse> listar)
    : ControllerBase
{
    /// <summary>Lista las estaciones; con <c>ruta</c> solo las de esa ruta, de la mas barata a la mas cara.</summary>
    [HttpGet]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsEstacionesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<clsEstacionesResponse>> Listar([FromQuery] string? ruta, CancellationToken ct = default)
        => Ok(await listar.Handle(new clsListarEstacionesQuery(ruta), ct));
}
