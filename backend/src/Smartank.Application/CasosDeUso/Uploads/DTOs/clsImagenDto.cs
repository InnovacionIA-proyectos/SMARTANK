namespace Smartank.Application.CasosDeUso.Uploads.DTOs;

/// <summary>Imagen para servir: tipo real (detectado por su firma) y bytes.</summary>
public sealed record clsImagenDto(string ContentType, byte[] Datos);
