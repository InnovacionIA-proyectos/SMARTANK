using Smartank.Domain.Autorizaciones.Reglas;

namespace Smartank.Application.CasosDeUso.Autonomia.DTOs;

/// <summary>Resultado de calcular la autonomia de un viaje (ya persistida en el viaje).</summary>
/// <param name="Viaje">Viaje con la autonomia estimada actualizada.</param>
/// <param name="AutonomiaKm">Autonomia en km, redondeada a 2 decimales.</param>
/// <param name="BaseRendimiento">De donde sale el rendimiento: historico, esperado o default.</param>
/// <param name="RendimientoUsado">Rendimiento (km por galon) usado en el calculo.</param>
/// <param name="Tolerancia">Fraccion 0-1 aplicada hacia abajo.</param>
public sealed record clsAutonomiaDto(
    clsViajeAutonomiaDto Viaje,
    double AutonomiaKm,
    BaseRendimiento BaseRendimiento,
    double RendimientoUsado,
    double Tolerancia);
