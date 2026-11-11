using Smartank.Application.CasosDeUso.Autonomia.DTOs;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Application.CasosDeUso.Autonomia.Mappers;

public static class clsViajeAutonomiaMapper
{
    public static clsViajeAutonomiaDto ToDto(clsViaje v) => new(
        v.Id,
        v.VehiculoId,
        v.Ruta,
        v.Estado,
        v.AutonomiaEstimadaKm,
        v.DespachadoPor,
        v.Manifiesto,
        v.InicioAt,
        v.FoId,
        v.PlacaCabezote,
        v.PlacaTrailer,
        v.ProgramaCabezote,
        v.CedulaConductor,
        v.Transportista,
        v.NumeroFo,
        v.CodigoRuta,
        v.NombreRuta,
        v.CiudadOrigen,
        v.CiudadDestino,
        v.PesoFo,
        v.GalonesEds,
        v.GalonesCredito,
        v.GalonesContado,
        v.GalonesRuta,
        v.KmtsRuta,
        v.FechaDespacho,
        v.GastosGenerados,
        v.GastosEnviadoSap,
        v.ConsumoEnviadoSap,
        v.EnviadoSap,
        v.Borrado,
        v.CreatedAt,
        v.UpdatedAt);
}
