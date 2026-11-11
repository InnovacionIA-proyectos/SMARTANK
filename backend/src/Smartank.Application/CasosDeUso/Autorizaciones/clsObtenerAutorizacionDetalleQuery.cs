using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Autorizaciones;

/// <summary>Detalle de una autorizacion con estacion, placa, viaje, conductor, telemetria y ruta.</summary>
public sealed record clsObtenerAutorizacionDetalleQuery(Guid AutorizacionId) : IQuery<clsAutorizacionDetalleDto>;

public sealed class clsObtenerAutorizacionDetalleQueryHandler(
    IAutorizacionesConsultas consultas,
    IOptions<clsParametrosNegocio> parametros)
    : IQueryHandler<clsObtenerAutorizacionDetalleQuery, clsAutorizacionDetalleDto>
{
    public async Task<clsAutorizacionDetalleDto> Handle(clsObtenerAutorizacionDetalleQuery query, CancellationToken ct = default) =>
        await consultas.ObtenerDetalleAsync(query.AutorizacionId, parametros.Value.RendimientoDefaultKmGal, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "Autorización no encontrada.");
}
