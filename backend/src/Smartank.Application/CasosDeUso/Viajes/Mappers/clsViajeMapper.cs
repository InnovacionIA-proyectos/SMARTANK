using Smartank.Application.CasosDeUso.Viajes.DTOs;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Domain.Vehiculos.Reglas;

namespace Smartank.Application.CasosDeUso.Viajes.Mappers;

public static class clsViajeMapper
{
    public static clsViajeDto ADto(clsViajeListadoFila f, double rendimientoDefault) => new(
        f.Id, f.VehiculoId, f.Placa, f.TipoMotor, f.Ruta, f.NombreRuta, f.CodigoRuta, f.Estado, f.AutonomiaEstimadaKm,
        f.NivelTanqueActual, f.CapacidadTanqueGal, f.UbicacionAt, f.NivelAt,
        clsAutonomiaActual.Calcular(f.NivelTanqueActual, f.RendimientoHistorico, f.RendimientoEsperado, rendimientoDefault, f.ToleranciaMotor),
        f.RutaKm, f.VehiculoLat, f.VehiculoLng, f.OrigenLat, f.OrigenLng, f.DestinoLat, f.DestinoLng,
        f.Manifiesto, f.InicioAt, f.FechaDespacho, f.RecomendacionActiva);

    public static clsRutaViajeDto ARutaDto(clsViajeRutaFila f, double rendimientoDefault) => new(
        new clsViajeResumenDto(f.Id, f.Ruta, f.Estado, f.Placa),
        f.Polilinea is null ? null : new clsRutaPolilineaDto(f.Polilinea, f.RutaKm, f.NumPuntos),
        f.VehiculoLat is { } lat && f.VehiculoLng is { } lng ? new clsVehiculoPosicionDto(f.Placa, lat, lng, f.VehiculoUbicacionAt) : null,
        clsAutonomiaActual.Calcular(f.NivelTanqueActual, f.RendimientoHistorico, f.RendimientoEsperado, rendimientoDefault, f.ToleranciaMotor));
}
