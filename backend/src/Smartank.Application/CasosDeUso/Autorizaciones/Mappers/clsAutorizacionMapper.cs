using System.Text.Json;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Domain.Autorizaciones.Entidades;

namespace Smartank.Application.CasosDeUso.Autorizaciones.Mappers;

public static class clsAutorizacionMapper
{
    public static clsAutorizacionDto ToDto(clsAutorizacionTanqueo a) => new(
        a.Id,
        a.ViajeId,
        a.VehiculoId,
        a.EstacionId,
        a.GalonesMax,
        a.PrecioTecho,
        a.DeltaAhorroEstimado,
        a.FechaInicio,
        a.FechaFin,
        a.Estado,
        a.AutorizadoPor,
        LeerJson(a.EdsConfirmacion),
        a.CreatedAt,
        a.UpdatedAt);

    /// <summary>La confirmacion de la EDS se guarda como texto JSON; el API la expone como objeto. Null si no hay o no es JSON valido.</summary>
    private static JsonElement? LeerJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
