using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Viajes;
using Smartank.Application.CasosDeUso.Viajes.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Viajes;

/// <summary>Consulta de viajes activos y de la ruta de cada uno, para los paneles de despacho y seguimiento (cualquier usuario con sesion).</summary>
[ApiController]
[Authorize]
[Route("api/viajes")]
public class clsViajesLecturaController(
    IQueryHandler<clsListarViajesQuery, clsViajesResponse> listar,
    IQueryHandler<clsObtenerRutaViajeQuery, clsRutaViajeDto> obtenerRuta)
    : ControllerBase
{
    /// <summary>Lista los viajes no cerrados con datos del vehiculo y la ruta. Los filtros se aplican en el servidor.</summary>
    /// <param name="estado">programado, en_ruta o descartado.</param>
    /// <param name="ruta">Nombre de ruta exacto.</param>
    /// <param name="placa">Fragmento de placa.</param>
    /// <param name="manifiesto">Fragmento de manifiesto.</param>
    /// <param name="anio">Anio de la fecha del viaje.</param>
    /// <param name="mes">Mes (1-12) de la fecha del viaje.</param>
    /// <param name="desplazamiento">vacio o cargado.</param>
    /// <param name="conRecomendacion">Query <c>con_recomendacion</c>: "1" para solo los viajes con una autorizacion de tanqueo vigente.</param>
    /// <param name="telemetria">si (nivel leido en la ultima hora) o no.</param>
    /// <param name="ct">Cancelacion de la peticion.</param>
    [HttpGet]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsViajesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<clsViajesResponse>> Listar(
        [FromQuery] string? estado,
        [FromQuery] string? ruta,
        [FromQuery] string? placa,
        [FromQuery] string? manifiesto,
        [FromQuery] string? anio,
        [FromQuery] string? mes,
        [FromQuery] string? desplazamiento,
        [FromQuery(Name = "con_recomendacion")] string? conRecomendacion,
        [FromQuery] string? telemetria,
        CancellationToken ct = default)
        => Ok(await listar.Handle(
            new clsListarViajesQuery(estado, ruta, placa, manifiesto, anio, mes, desplazamiento, conRecomendacion, telemetria), ct));

    /// <summary>Polilinea de la ruta del viaje con la posicion y la autonomia actual del vehiculo.</summary>
    [HttpGet("{id}/ruta")]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsRutaViajeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<clsRutaViajeDto>> ObtenerRuta(Guid id, CancellationToken ct = default)
        => Ok(await obtenerRuta.Handle(new clsObtenerRutaViajeQuery(id), ct));
}
