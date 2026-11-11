using Smartank.Application.CasosDeUso.Telemetria.DTOs;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Application.CasosDeUso.Telemetria.Mappers;

public static class clsVehiculoMapper
{
    public static clsVehiculoDto ADto(clsVehiculo v) => new(
        v.Id, v.Placa, v.TipoMotor, v.NivelTanqueActual, v.RendimientoHistorico, v.RendimientoEsperado, v.Odometro,
        v.ToleranciaMotor, v.UltimaLat, v.UltimaLng, v.Programa, v.FpsapId, v.CapacidadTanqueGal, v.GalonesReserva,
        v.GalonesUtilizables, v.GalonesTolerancia, v.ChipTanqueo, v.NitPropietario, v.AliasPlaca, v.Activo,
        v.GeotabDeviceId, v.CreatedAt, v.UpdatedAt);

    /// <summary>
    /// "ActualizadoAt" es el momento de la ultima actualizacion del registro del vehiculo (igual que el origen), no el de
    /// la lectura del tanque: para eso el vehiculo guarda NivelAt, que el listado de viajes expone por separado.
    /// </summary>
    public static clsTelemetriaVehiculoDto ATelemetria(clsVehiculo v) => new(
        v.Id, v.Placa, v.NivelTanqueActual, v.Odometro, v.NivelTanqueActual is not null, v.UpdatedAt);
}
