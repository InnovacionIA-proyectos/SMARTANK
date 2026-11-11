using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Domain.Alertas.Entidades;

namespace Smartank.Application.CasosDeUso.Alertas.Mappers;

public static class clsAlertaMapper
{
    public static clsAlertaDto ADto(clsAlerta a) => new(
        a.Id, a.Tipo, a.VehiculoId, a.EventoId, a.MagnitudDesviacion, a.Estado, a.GestionadaPor, a.CreatedAt, a.UpdatedAt);
}
