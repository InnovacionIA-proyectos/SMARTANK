namespace Smartank.Application.CasosDeUso.Eventos.DTOs;

/// <summary>Historial de tanqueos combinado (EventosResponse del origen).</summary>
public sealed record clsHistorialTanqueosDto(int Total, IReadOnlyList<clsHistorialTanqueoItemDto> Eventos);
