using Smartank.Application.CasosDeUso.Telemetria.DTOs;
using Smartank.Application.CasosDeUso.Telemetria.Interfaces;
using Smartank.Application.CasosDeUso.Telemetria.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Telemetria;

/// <summary>Ultimo estado de tanque de un vehiculo; <c>TieneTelemetria</c> es false si nunca ha reportado nivel.</summary>
public sealed record clsObtenerTelemetriaVehiculoQuery(Guid VehiculoId) : IQuery<clsTelemetriaVehiculoDto>;

public sealed class clsObtenerTelemetriaVehiculoQueryHandler(ITelemetriaRepository vehiculos)
    : IQueryHandler<clsObtenerTelemetriaVehiculoQuery, clsTelemetriaVehiculoDto>
{
    public async Task<clsTelemetriaVehiculoDto> Handle(clsObtenerTelemetriaVehiculoQuery query, CancellationToken ct = default)
    {
        var vehiculo = await vehiculos.ObtenerVehiculoAsync(query.VehiculoId, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "Vehículo no encontrado.");

        return clsVehiculoMapper.ATelemetria(vehiculo);
    }
}
