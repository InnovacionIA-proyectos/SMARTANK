using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;

namespace Smartank.Application.CasosDeUso.Recomendacion.DTOs;

/// <summary>Resultado de recomendar un tanqueo (RecomendacionResponse del origen).</summary>
/// <param name="RequiereTanqueo">False cuando la autonomia cubre la ruta restante (la propuesta se genera igual para abrir el panel).</param>
/// <param name="AutonomiaKm">Autonomia del viaje en km, a 2 decimales.</param>
/// <param name="RutaKmRestante">Km que faltan sobre la ruta hasta el destino, a 1 decimal; null si no se pudo medir.</param>
/// <param name="Motivo">Por que no hace falta tanquear (null si hace falta).</param>
/// <param name="Autorizacion">Propuesta creada o actualizada.</param>
/// <param name="EstacionElegida">EDS mas barata del conjunto considerado.</param>
/// <param name="GalonesMax">Galones propuestos.</param>
/// <param name="PrecioTecho">Precio por galon de la EDS elegida.</param>
/// <param name="DeltaAhorroEstimado">Ahorro frente a la EDS mas cara considerada.</param>
/// <param name="EstacionesCandidatas">Cuantas EDS se compararon.</param>
/// <param name="SobreRuta">True si el calculo se hizo sobre la ruta real (no en linea recta).</param>
public sealed record clsRecomendacionDto(
    bool RequiereTanqueo,
    double AutonomiaKm,
    double? RutaKmRestante,
    string? Motivo,
    clsAutorizacionDto Autorizacion,
    clsEstacionElegidaDto EstacionElegida,
    decimal GalonesMax,
    decimal PrecioTecho,
    decimal DeltaAhorroEstimado,
    int EstacionesCandidatas,
    bool SobreRuta);
