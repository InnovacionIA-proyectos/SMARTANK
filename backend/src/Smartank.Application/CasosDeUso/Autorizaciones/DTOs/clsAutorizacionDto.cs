using System.Text.Json;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones.DTOs;

/// <summary>Autorizacion de tanqueo tal como la expone el API (serializeAutorizacion del origen).</summary>
public sealed record clsAutorizacionDto(
    Guid Id,
    Guid ViajeId,
    Guid VehiculoId,
    Guid EstacionId,
    decimal GalonesMax,
    decimal PrecioTecho,
    decimal? DeltaAhorroEstimado,
    DateTimeOffset FechaInicio,
    DateTimeOffset FechaFin,
    EstadoAutorizacion Estado,
    Guid? AutorizadoPor,
    JsonElement? EdsConfirmacion,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
