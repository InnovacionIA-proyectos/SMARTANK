using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Alertas.DTOs;

/// <summary>Alerta de control (misma forma que el serializador del origen).</summary>
public record clsAlertaDto(
    Guid Id,
    TipoAlerta Tipo,
    Guid VehiculoId,
    Guid? EventoId,
    double? MagnitudDesviacion,
    EstadoAlerta Estado,
    Guid? GestionadaPor,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>Fila de la bandeja: la alerta mas la placa del vehiculo.</summary>
public record clsAlertaListadoDto(
    Guid Id,
    TipoAlerta Tipo,
    Guid VehiculoId,
    Guid? EventoId,
    double? MagnitudDesviacion,
    EstadoAlerta Estado,
    Guid? GestionadaPor,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Placa);

/// <summary>Respuesta de GET /api/alertas: { estado, total, alertas }. <c>Estado</c> repite el filtro aplicado (null si no hubo).</summary>
public record clsAlertasResponse(EstadoAlerta? Estado, int Total, IReadOnlyList<clsAlertaListadoDto> Alertas);
