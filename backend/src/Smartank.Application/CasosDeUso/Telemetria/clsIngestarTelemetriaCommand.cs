using Smartank.Application.CasosDeUso.Telemetria.DTOs;
using Smartank.Application.CasosDeUso.Telemetria.Interfaces;
using Smartank.Application.CasosDeUso.Telemetria.Mappers;
using Smartank.Application.CasosDeUso.Telemetria.Validadores;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Telemetria;

/// <summary>Recibe una lectura de nivel/odometro/posicion del proveedor de telemetria (autenticada por secreto de servicio, no por sesion).</summary>
public sealed record clsIngestarTelemetriaCommand(
    string? Placa,
    decimal? NivelGalones,
    double? Odometro,
    double? Lat,
    double? Lng,
    DateTimeOffset? LeidoAt) : ICommand<clsIngestaTelemetriaResponse>;

public sealed class clsIngestarTelemetriaCommandHandler(ITelemetriaRepository vehiculos, TimeProvider reloj)
    : ICommandHandler<clsIngestarTelemetriaCommand, clsIngestaTelemetriaResponse>
{
    public async Task<clsIngestaTelemetriaResponse> Handle(clsIngestarTelemetriaCommand command, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        var placa = clsIngestaTelemetriaValidador.ValidarYNormalizar(
            command.Placa, command.NivelGalones, command.Odometro, command.Lat, command.Lng, command.LeidoAt, ahora);

        var vehiculo = await vehiculos.ActualizarTelemetriaAsync(
            placa, command.NivelGalones!.Value, command.Odometro, command.Lat, command.Lng, command.LeidoAt ?? ahora, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", $"No existe un vehículo con placa {placa}.");

        return new clsIngestaTelemetriaResponse(true, clsVehiculoMapper.ADto(vehiculo));
    }
}
