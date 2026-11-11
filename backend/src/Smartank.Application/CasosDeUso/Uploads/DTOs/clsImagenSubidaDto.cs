namespace Smartank.Application.CasosDeUso.Uploads.DTOs;

/// <summary>Imagen guardada: su id y la URL con la que el personal la consulta.</summary>
public sealed record clsImagenSubidaDto(Guid Id, string Url);
