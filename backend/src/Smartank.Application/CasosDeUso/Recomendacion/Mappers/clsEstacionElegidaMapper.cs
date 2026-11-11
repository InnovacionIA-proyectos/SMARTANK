using Smartank.Application.CasosDeUso.Recomendacion.DTOs;
using Smartank.Domain.Estaciones.Entidades;

namespace Smartank.Application.CasosDeUso.Recomendacion.Mappers;

public static class clsEstacionElegidaMapper
{
    public static clsEstacionElegidaDto ToDto(clsEstacion e) => new(
        e.Id,
        e.Nombre,
        e.Ruta,
        e.Lat,
        e.Lng,
        e.PrecioGalonActual,
        e.PrecioAcpm,
        e.PrecioCorriente,
        e.PrecioActualizadoAt,
        e.SoportaPreautorizacion,
        e.Proveedor,
        e.Producto,
        e.CodigoExterno,
        e.CreatedAt,
        e.UpdatedAt);
}
