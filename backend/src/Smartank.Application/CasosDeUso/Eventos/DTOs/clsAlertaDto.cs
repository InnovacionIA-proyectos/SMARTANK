using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Eventos.DTOs;

/// <summary>Alerta generada por la verificacion de una carga (serializeAlerta del origen).</summary>
public sealed record clsAlertaDto(
    Guid Id,
    TipoAlerta Tipo,
    Guid VehiculoId,
    Guid? EventoId,
    double? MagnitudDesviacion,
    EstadoAlerta Estado,
    Guid? GestionadaPor,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
