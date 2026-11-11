using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Eventos.DTOs;

/// <summary>Resultado de verificar una carga: estado final de la autorizacion y alertas creadas.</summary>
public sealed record clsVerificacionDto(
    Guid EventoId,
    EstadoAutorizacion EstadoAutorizacion,
    int AlertasGeneradas,
    IReadOnlyList<clsAlertaDto> Alertas);
