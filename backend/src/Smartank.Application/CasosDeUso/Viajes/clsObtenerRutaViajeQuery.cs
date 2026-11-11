using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Viajes.DTOs;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Application.CasosDeUso.Viajes.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Viajes;

/// <summary>Polilinea de la ruta de un viaje mas la posicion y la autonomia del vehiculo, para pintar el tramo de autonomia en el mapa.</summary>
public sealed record clsObtenerRutaViajeQuery(Guid ViajeId) : IQuery<clsRutaViajeDto>;

public sealed class clsObtenerRutaViajeQueryHandler(IViajesLecturaRepository viajes, IOptions<clsParametrosNegocio> parametros)
    : IQueryHandler<clsObtenerRutaViajeQuery, clsRutaViajeDto>
{
    public async Task<clsRutaViajeDto> Handle(clsObtenerRutaViajeQuery query, CancellationToken ct = default)
    {
        var fila = await viajes.ObtenerRutaAsync(query.ViajeId, ct)
            ?? throw new clsNotFoundException("VIAJE_NO_ENCONTRADO", "Viaje no encontrado.");

        return clsViajeMapper.ARutaDto(fila, parametros.Value.RendimientoDefaultKmGal);
    }
}
