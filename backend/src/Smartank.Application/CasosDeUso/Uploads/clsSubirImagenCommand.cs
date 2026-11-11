using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.CasosDeUso.Uploads.DTOs;
using Smartank.Application.CasosDeUso.Uploads.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Uploads.Entidades;
using Smartank.Domain.Uploads.Reglas;

namespace Smartank.Application.CasosDeUso.Uploads;

/// <summary>
/// El conductor sube la foto del surtidor con el token de su enlace (sin sesion). Maximo 5 MB, solo JPEG/PNG/WEBP reconocidos por
/// su firma real (el Content-Type declarado no basta) y la autorizacion del token debe estar Autorizada.
/// </summary>
/// <param name="Token">Token de la bitacora.</param>
/// <param name="ContentType">Content-Type declarado por el cliente (debe ser image/*).</param>
/// <param name="Datos">Bytes de la imagen.</param>
/// <param name="UrlBaseSolicitud">Origen de la peticion; se usa para armar la URL si no hay App__UrlPublica.</param>
public sealed record clsSubirImagenCommand(string? Token, string? ContentType, byte[] Datos, string UrlBaseSolicitud)
    : ICommand<clsImagenSubidaDto>;

public sealed class clsSubirImagenCommandHandler(
    IBitacoraTokenPort tokens,
    IAutorizacionesRepository autorizaciones,
    IUploadsRepository uploads,
    IUnitOfWork uow,
    IOptions<clsAplicacionOptions> aplicacion)
    : ICommandHandler<clsSubirImagenCommand, clsImagenSubidaDto>
{
    /// <summary>Tamano maximo de la imagen: 5 MB.</summary>
    public const int MAX_BYTES = 5 * 1024 * 1024;

    public async Task<clsImagenSubidaDto> Handle(clsSubirImagenCommand command, CancellationToken ct = default)
    {
        // Primero la credencial (una peticion anonima sin token valido no debe enterarse de mas) y luego las validaciones baratas.
        var autorizacionId = tokens.Validar(command.Token ?? string.Empty);

        if (command.ContentType is null || !command.ContentType.TrimStart().StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            throw new clsValidationFailedException("ARCHIVO_NO_ES_IMAGEN", "El archivo debe ser una imagen.");
        if (command.Datos.Length == 0)
            throw new clsValidationFailedException("ARCHIVO_VACIO", "Archivo vacío.");
        if (command.Datos.Length > MAX_BYTES)
            throw new clsValidationFailedException("ARCHIVO_DEMASIADO_GRANDE", "La imagen supera el máximo (5 MB).");

        // El tipo se decide por los bytes, no por lo que declare el cliente; es el que se guarda y se sirve despues.
        var tipoReal = clsFirmaImagen.Detectar(command.Datos)
            ?? throw new clsValidationFailedException(
                "IMAGEN_INVALIDA", "El contenido no es una imagen JPEG, PNG o WEBP válida.");

        var autorizacion = await autorizaciones.ObtenerAsync(autorizacionId, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "La autorización del enlace no existe.");
        if (autorizacion.Estado != EstadoAutorizacion.Autorizada)
            throw new clsConflictException(
                "AUTORIZACION_NO_AUTORIZADA",
                $"La autorización no está autorizada (estado actual: {clsSnakeCase.ToSnake(autorizacion.Estado)}).");

        var upload = new clsUpload { ContentType = tipoReal, Data = command.Datos };
        uploads.Agregar(upload);
        await uow.SaveChangesAsync(ct);

        var baseUrl = clsTexto.NullifyBlank(aplicacion.Value.UrlPublica)?.TrimEnd('/') ?? command.UrlBaseSolicitud.TrimEnd('/');
        return new clsImagenSubidaDto(upload.Id, $"{baseUrl}/api/uploads/{upload.Id}");
    }
}
