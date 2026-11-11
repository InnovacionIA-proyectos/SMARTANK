using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Uploads;
using Smartank.Application.CasosDeUso.Uploads.DTOs;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Api.Uploads;

/// <summary>Fotos del surtidor: el conductor las sube con el token de su bitacora; el personal las consulta con sesion.</summary>
[ApiController]
[Authorize]
[Route("api/uploads")]
public class clsUploadsController(
    ICommandHandler<clsSubirImagenCommand, clsImagenSubidaDto> subir,
    IQueryHandler<clsObtenerImagenQuery, clsImagenDto> obtener)
    : ControllerBase
{
    private const int TAMANO_BUFFER = 81920;

    /// <summary>
    /// Sube una imagen (foto del surtidor): el cuerpo son los bytes crudos y el Content-Type debe ser image/*. Maximo 5 MB; solo
    /// JPEG, PNG y WEBP reconocidos por su firma. Sin sesion: se identifica con <c>?token=</c> de la bitacora.
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaEscrituraPublica)]
    [ProducesResponseType(typeof(clsImagenSubidaDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<clsImagenSubidaDto>> Subir([FromQuery] string? token, CancellationToken ct = default)
    {
        var datos = await LeerCuerpoAcotadoAsync(ct);
        var urlBase = $"{Request.Scheme}://{Request.Host}";
        var dto = await subir.Handle(new clsSubirImagenCommand(token, Request.ContentType, datos, urlBase), ct);
        return Created($"/api/uploads/{dto.Id}", dto);
    }

    /// <summary>Sirve la imagen almacenada con su tipo real. Requiere sesion (cualquier rol); no se cachea en caches compartidas.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct = default)
    {
        var imagen = await obtener.Handle(new clsObtenerImagenQuery(id), ct);
        // private: la foto es un dato interno; immutable: una imagen guardada nunca cambia, el navegador puede conservarla.
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(imagen.Datos, imagen.ContentType);
    }

    /// <summary>
    /// Lee el cuerpo sin pasar de 5 MB (+1 byte para detectar el exceso): un cliente anonimo no puede obligar al servidor a
    /// cargar en memoria un cuerpo arbitrariamente grande.
    /// </summary>
    private async Task<byte[]> LeerCuerpoAcotadoAsync(CancellationToken ct)
    {
        const int MAX = clsSubirImagenCommandHandler.MAX_BYTES;
        if (Request.ContentLength > MAX) throw Demasiado();

        using var memoria = new MemoryStream();
        var buffer = new byte[TAMANO_BUFFER];
        int leidos;
        while ((leidos = await Request.Body.ReadAsync(buffer, ct)) > 0)
        {
            if (memoria.Length + leidos > MAX) throw Demasiado();
            memoria.Write(buffer, 0, leidos);
        }
        return memoria.ToArray();
    }

    private static clsValidationFailedException Demasiado() =>
        new("ARCHIVO_DEMASIADO_GRANDE", "La imagen supera el máximo (5 MB).");
}
