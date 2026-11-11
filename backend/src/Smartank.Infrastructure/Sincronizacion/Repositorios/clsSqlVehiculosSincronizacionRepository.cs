using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>Escrituras masivas y lecturas de tblVehiculos para las sincronizaciones (SQL directo: tabla temporal + SqlBulkCopy).</summary>
public sealed class clsSqlVehiculosSincronizacionRepository(clsConexionSmartank conexion) : IVehiculosSincronizacionRepository
{
    private sealed record clsFilaPlaca(Guid Id, string Placa);

    private sealed record clsFilaBasica(Guid Id, string Placa, decimal? CapacidadTanqueGal);

    private static clsEspecMasiva EspecUpsert()
    {
        string[] datos =
        [
            "TipoMotor", "FpsapId", "CapacidadTanqueGal", "GalonesReserva", "GalonesUtilizables", "GalonesTolerancia",
            "ChipTanqueo", "NitPropietario", "AliasPlaca", "Activo",
        ];
        return new clsEspecMasiva(
            clsConexionSmartank.Tabla("tblVehiculos"),
            [
                new("Placa", typeof(string)), new("TipoMotor", typeof(string)), new("FpsapId", typeof(long)),
                new("CapacidadTanqueGal", typeof(decimal)), new("GalonesReserva", typeof(decimal)),
                new("GalonesUtilizables", typeof(decimal)), new("GalonesTolerancia", typeof(decimal)),
                new("ChipTanqueo", typeof(string)), new("NitPropietario", typeof(string)), new("AliasPlaca", typeof(string)),
                new("Activo", typeof(bool)),
            ],
            ["Placa"],
            [.. clsAsignacionMasiva.Directas(datos), new clsAsignacionMasiva("FpsapSincronizadoAt", "SYSDATETIMEOFFSET()")],
            [.. clsAsignacionMasiva.Directas(["Placa", .. datos]), new clsAsignacionMasiva("FpsapSincronizadoAt", "SYSDATETIMEOFFSET()")]);
    }

    private static clsEspecMasiva EspecTelemetria()
    {
        string[] datos = ["UltimaLat", "UltimaLng", "UbicacionAt", "NivelTanqueActual", "NivelAt", "Odometro", "GeotabDeviceId"];
        return new clsEspecMasiva(
            clsConexionSmartank.Tabla("tblVehiculos"),
            [
                new("Id", typeof(Guid)), new("UltimaLat", typeof(decimal)), new("UltimaLng", typeof(decimal)),
                new("UbicacionAt", typeof(DateTimeOffset)), new("NivelTanqueActual", typeof(decimal)),
                new("NivelAt", typeof(DateTimeOffset)), new("Odometro", typeof(decimal)), new("GeotabDeviceId", typeof(string)),
            ],
            ["Id"],
            // Un dato nuevo nulo conserva el almacenado: un vehiculo sin lectura nueva mantiene la ultima conocida.
            [.. datos.Select(c => new clsAsignacionMasiva(c, $"COALESCE(s.[{c}], t.[{c}])"))],
            []);
    }

    private static clsEspecMasiva EspecRendimiento() => new(
        clsConexionSmartank.Tabla("tblVehiculos"),
        [new("Id", typeof(Guid)), new("RendimientoHistorico", typeof(decimal))],
        ["Id"],
        clsAsignacionMasiva.Directas("RendimientoHistorico"),
        []);

    public async Task<clsResultadoUpsertDto> UpsertDesdeLegadoAsync(IReadOnlyList<clsCabezoteLegadoDto> vehiculos, CancellationToken ct = default)
    {
        var filas = vehiculos.Select(v => new object?[]
        {
            v.Placa.Trim(), v.TipoMotor, v.FpsapId,
            clsValoresMasivos.Decimal(v.CapacidadTanqueGal, 3), clsValoresMasivos.Decimal(v.GalonesReserva, 3),
            clsValoresMasivos.Decimal(v.GalonesUtilizables, 3), clsValoresMasivos.Decimal(v.GalonesTolerancia, 3),
            clsValoresMasivos.Texto(v.ChipTanqueo), clsValoresMasivos.Texto(v.NitPropietario), clsValoresMasivos.Texto(v.AliasPlaca),
            v.Activo,
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, EspecUpsert(), filas, f => (string)f[0]!, ct);
    }

    public async Task<IReadOnlyDictionary<string, Guid>> ObtenerMapaPlacaAIdAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirAsync(ct);
        var filas = await cn.QueryAsync<clsFilaPlaca>(new CommandDefinition(
            $"SELECT Id, Placa FROM {clsConexionSmartank.Tabla("tblVehiculos")}", cancellationToken: ct));
        var mapa = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas) mapa[f.Placa.Trim()] = f.Id;
        return mapa;
    }

    public async Task<IReadOnlyList<clsVehiculoBasicoDto>> ListarBasicosAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirAsync(ct);
        var filas = await cn.QueryAsync<clsFilaBasica>(new CommandDefinition(
            $"SELECT Id, Placa, CapacidadTanqueGal FROM {clsConexionSmartank.Tabla("tblVehiculos")}", cancellationToken: ct));
        return filas.Select(f => new clsVehiculoBasicoDto(f.Id, f.Placa, f.CapacidadTanqueGal)).ToList();
    }

    public async Task<clsResultadoUpsertDto> ActualizarTelemetriaAsync(IReadOnlyList<clsActualizacionTelemetriaDto> telemetria, CancellationToken ct = default)
    {
        var filas = telemetria.Select(t => new object?[]
        {
            t.VehiculoId,
            clsValoresMasivos.Decimal(t.Lat, 7), clsValoresMasivos.Decimal(t.Lng, 7), t.UbicacionAt,
            clsValoresMasivos.Decimal(t.NivelGalones, 3), t.NivelAt,
            clsValoresMasivos.Decimal(t.OdometroKm, 3), clsValoresMasivos.Texto(t.GeotabDeviceId),
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, EspecTelemetria(), filas, f => f[0]!.ToString()!, ct);
    }

    public async Task<clsResultadoUpsertDto> ActualizarRendimientoAsync(IReadOnlyList<clsRendimientoVehiculoDto> rendimientos, CancellationToken ct = default)
    {
        var filas = rendimientos.Select(r => new object?[] { r.VehiculoId, clsValoresMasivos.Decimal(r.KmPorGalon, 4) }).ToList();
        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, EspecRendimiento(), filas, f => f[0]!.ToString()!, ct);
    }
}
