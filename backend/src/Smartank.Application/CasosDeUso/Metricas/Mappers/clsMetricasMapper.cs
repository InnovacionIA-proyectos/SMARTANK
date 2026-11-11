using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Domain.Metricas.Reglas;

namespace Smartank.Application.CasosDeUso.Metricas.Mappers;

public static class clsMetricasMapper
{
    public static clsMetricasRecuperacionDto ADto(
        clsRecuperacionFiltrosDto filtros, clsResultadoRecuperacion r, clsOpcionesFiltroDto opciones) => new(
        filtros,
        r.Totales.Galones,
        r.Totales.Tanqueos,
        r.Totales.GastoReal,
        r.Totales.GastoIdeal,
        r.Totales.GastoMaximo,
        r.Totales.AhorroVsMaximo,
        r.Totales.OportunidadVsIdeal,
        r.Totales.PrecioMinimoRuta,
        r.Utilizacion.Select(u => new clsUtilizacionEdsDto(u.Proveedor, u.Tanqueos, u.Galones)).ToList(),
        r.Equipos.Select(e => new clsEquipoFilaDto(e.Equipo, e.Mes, e.Fecha, e.ValorGalon, e.Galones, e.ValorTotal, e.Eds, e.Ciudad)).ToList(),
        r.GalonesPorEds.Select(g => new clsGalonesPorEdsDto(g.Id, g.Nombre, g.Lat, g.Lng, g.Ruta, g.Proveedor, g.Precio, g.Galones, g.Tanqueos)).ToList(),
        r.Rutas.Select(l => new clsRutaLineaDto(l.Ruta, l.OrigenLat, l.OrigenLng, l.DestinoLat, l.DestinoLng)).ToList(),
        opciones);
}
