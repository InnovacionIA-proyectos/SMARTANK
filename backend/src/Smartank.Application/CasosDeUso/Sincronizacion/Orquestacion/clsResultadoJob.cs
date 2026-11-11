using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

/// <summary>Resultado de ejecutar un job: estado, duracion y, segun el caso, los contadores o el motivo del fallo.</summary>
public sealed record clsResultadoJob(
    string Nombre, EstadoJob Estado, TimeSpan Duracion, clsResultadoSincronizacion? Resultado, string? Detalle);
