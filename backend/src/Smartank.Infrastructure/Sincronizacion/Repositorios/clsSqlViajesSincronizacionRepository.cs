using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Common;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>Upsert masivo de tblViajes por FoId y lectura de lo necesario para sincronizar las rutas.</summary>
public sealed class clsSqlViajesSincronizacionRepository(clsConexionSmartank conexion) : IViajesSincronizacionRepository
{
    private sealed record clsFilaViajeParaRuta(
        Guid ViajeId, string NumeroFo, string? CiudadOrigen, string? CiudadDestino, long? ValhallaIdAlmacenado, int TieneRuta);

    private static clsEspecMasiva Espec()
    {
        // Se actualiza todo lo que viene del flete; DespachadoPor y AutonomiaEstimadaKm los calcula la aplicacion y se conservan.
        string[] datos =
        [
            "VehiculoId", "Ruta", "Estado", "Manifiesto", "InicioAt", "PlacaCabezote", "PlacaTrailer", "ProgramaCabezote",
            "CedulaConductor", "Transportista", "NumeroFo", "CodigoRuta", "NombreRuta", "CiudadOrigen", "CiudadDestino",
            "PesoFo", "GalonesEds", "GalonesCredito", "GalonesContado", "GalonesRuta", "KmtsRuta", "FechaDespacho",
            "GastosGenerados", "GastosEnviadoSap", "ConsumoEnviadoSap", "EnviadoSap", "Borrado",
        ];
        return new clsEspecMasiva(
            clsConexionSmartank.Tabla("tblViajes"),
            [
                new("FoId", typeof(long)), new("VehiculoId", typeof(Guid)), new("Ruta", typeof(string)), new("Estado", typeof(string)),
                new("Manifiesto", typeof(string)), new("InicioAt", typeof(DateTimeOffset)), new("PlacaCabezote", typeof(string)),
                new("PlacaTrailer", typeof(string)), new("ProgramaCabezote", typeof(string)), new("CedulaConductor", typeof(string)),
                new("Transportista", typeof(string)), new("NumeroFo", typeof(string)), new("CodigoRuta", typeof(string)),
                new("NombreRuta", typeof(string)), new("CiudadOrigen", typeof(string)), new("CiudadDestino", typeof(string)),
                new("PesoFo", typeof(decimal)), new("GalonesEds", typeof(decimal)), new("GalonesCredito", typeof(decimal)),
                new("GalonesContado", typeof(decimal)), new("GalonesRuta", typeof(decimal)), new("KmtsRuta", typeof(decimal)),
                new("FechaDespacho", typeof(DateTimeOffset)), new("GastosGenerados", typeof(bool)), new("GastosEnviadoSap", typeof(bool)),
                new("ConsumoEnviadoSap", typeof(bool)), new("EnviadoSap", typeof(bool)), new("Borrado", typeof(bool)),
            ],
            ["FoId"],
            [.. clsAsignacionMasiva.Directas(datos), new clsAsignacionMasiva("FoSincronizadoAt", "SYSDATETIMEOFFSET()")],
            [.. clsAsignacionMasiva.Directas(["FoId", .. datos]), new clsAsignacionMasiva("FoSincronizadoAt", "SYSDATETIMEOFFSET()")]);
    }

    public async Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsViajeUpsertDto> viajes, CancellationToken ct = default)
    {
        var filas = viajes.Select(v => new object?[]
        {
            v.FoId, v.VehiculoId, v.Ruta, clsSnakeCase.ToSnake(v.Estado), clsValoresMasivos.Texto(v.Manifiesto), v.InicioAt,
            v.PlacaCabezote, clsValoresMasivos.Texto(v.PlacaTrailer), clsValoresMasivos.Texto(v.ProgramaCabezote),
            clsValoresMasivos.Texto(v.CedulaConductor), clsValoresMasivos.Texto(v.Transportista), clsValoresMasivos.Texto(v.NumeroFo),
            clsValoresMasivos.Texto(v.CodigoRuta), clsValoresMasivos.Texto(v.NombreRuta), clsValoresMasivos.Texto(v.CiudadOrigen),
            clsValoresMasivos.Texto(v.CiudadDestino),
            clsValoresMasivos.Decimal(v.PesoFo, 3), clsValoresMasivos.Decimal(v.GalonesEds, 3), clsValoresMasivos.Decimal(v.GalonesCredito, 3),
            clsValoresMasivos.Decimal(v.GalonesContado, 3), clsValoresMasivos.Decimal(v.GalonesRuta, 3), clsValoresMasivos.Decimal(v.KmtsRuta, 3),
            v.FechaDespacho, v.GastosGenerados, v.GastosEnviadoSap, v.ConsumoEnviadoSap, v.EnviadoSap, v.Borrado,
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, Espec(), filas, f => $"FO {f[0]} ({f[6]})", ct);
    }

    public async Task<IReadOnlyList<clsViajeParaRutaDto>> ListarParaRutasAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirAsync(ct);
        var filas = await cn.QueryAsync<clsFilaViajeParaRuta>(new CommandDefinition(
            $"""
            SELECT v.Id AS ViajeId, v.NumeroFo, v.CiudadOrigen, v.CiudadDestino,
                   r.ValhallaId AS ValhallaIdAlmacenado,
                   CASE WHEN r.Id IS NOT NULL THEN 1 ELSE 0 END AS TieneRuta
            FROM {clsConexionSmartank.Tabla("tblViajes")} AS v
            LEFT JOIN {clsConexionSmartank.Tabla("tblRutas")} AS r ON r.ViajeId = v.Id
            WHERE v.NumeroFo IS NOT NULL AND v.NumeroFo <> N''
            """,
            commandTimeout: 120, cancellationToken: ct));
        return filas.Select(f => new clsViajeParaRutaDto(
            f.ViajeId, f.NumeroFo, f.CiudadOrigen, f.CiudadDestino, f.ValhallaIdAlmacenado, f.TieneRuta == 1)).ToList();
    }
}
