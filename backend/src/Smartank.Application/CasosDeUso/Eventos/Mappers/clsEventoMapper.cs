using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Eventos.Entidades;

namespace Smartank.Application.CasosDeUso.Eventos.Mappers;

public static class clsEventoMapper
{
    public static clsEventoDto ToDto(clsEventoTanqueo e) => new(
        e.Id,
        e.AutorizacionId,
        e.VehiculoId,
        e.EstacionId,
        e.GalonesCargados,
        e.PrecioPagado,
        e.FotoSurtidorUrl,
        e.OdometroEvento,
        e.NivelAntes,
        e.NivelDespues,
        e.RegistradoPor,
        e.Programa,
        e.Producto,
        e.OcurridoAt,
        e.CreatedAt,
        e.UpdatedAt);

    public static clsAlertaDto ToDto(clsAlerta a) => new(
        a.Id,
        a.Tipo,
        a.VehiculoId,
        a.EventoId,
        a.MagnitudDesviacion,
        a.Estado,
        a.GestionadaPor,
        a.CreatedAt,
        a.UpdatedAt);
}
