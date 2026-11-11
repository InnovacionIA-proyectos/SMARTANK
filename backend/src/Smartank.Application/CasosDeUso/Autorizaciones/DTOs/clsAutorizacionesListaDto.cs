namespace Smartank.Application.CasosDeUso.Autorizaciones.DTOs;

/// <summary>Listado de autorizaciones: total y detalles.</summary>
public sealed record clsAutorizacionesListaDto(int Total, IReadOnlyList<clsAutorizacionDetalleDto> Autorizaciones);
