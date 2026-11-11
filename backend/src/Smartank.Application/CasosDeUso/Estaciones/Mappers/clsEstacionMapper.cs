using Smartank.Application.CasosDeUso.Estaciones.DTOs;
using Smartank.Domain.Estaciones.Entidades;

namespace Smartank.Application.CasosDeUso.Estaciones.Mappers;

public static class clsEstacionMapper
{
    public static clsEstacionDto ADto(clsEstacion e) => new(
        e.Id, e.Nombre, e.Ruta, e.Lat, e.Lng, e.PrecioGalonActual, e.PrecioAcpm, e.PrecioCorriente, e.PrecioActualizadoAt,
        e.SoportaPreautorizacion, e.Proveedor, e.Producto, e.CodigoExterno, e.CreatedAt, e.UpdatedAt);
}
