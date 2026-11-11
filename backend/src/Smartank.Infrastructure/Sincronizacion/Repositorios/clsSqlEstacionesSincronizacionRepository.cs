using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>Lectura y escritura masiva de tblEstaciones para la sincronizacion de precios y la carga manual.</summary>
public sealed class clsSqlEstacionesSincronizacionRepository(clsConexionSmartank conexion) : IEstacionesSincronizacionRepository
{
    private sealed record clsFilaReferencia(Guid Id, string Nombre, decimal? Lat, decimal? Lng, string? CodigoExterno);

    private sealed record clsFilaCandidata(decimal Lat, decimal Lng, decimal PrecioAcpm);

    private sealed record clsFilaEstacion(
        Guid Id, string Nombre, string? Ruta, decimal? Lat, decimal? Lng, decimal? PrecioGalonActual, decimal? PrecioAcpm,
        decimal? PrecioCorriente, DateTimeOffset? PrecioActualizadoAt, bool SoportaPreautorizacion, string? Proveedor,
        string? Producto, string? CodigoExterno, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

    private static string Tabla => clsConexionSmartank.Tabla("tblEstaciones");

    private static clsEspecMasiva EspecActualizarPrecios() => new(
        Tabla,
        [new("Id", typeof(Guid)), new("PrecioAcpm", typeof(decimal)), new("PrecioCorriente", typeof(decimal))],
        ["Id"],
        [
            new clsAsignacionMasiva("PrecioAcpm"),
            new clsAsignacionMasiva("PrecioCorriente"),
            // El precio por galon actual es el ACPM (combustible de la flota); sin el, la corriente; sin ninguno se conserva.
            new clsAsignacionMasiva("PrecioGalonActual", "COALESCE(s.[PrecioAcpm], s.[PrecioCorriente], t.[PrecioGalonActual])"),
            new clsAsignacionMasiva("PrecioActualizadoAt", "SYSDATETIMEOFFSET()"),
        ],
        []);

    private static clsEspecMasiva EspecInsertar() => new(
        Tabla,
        [
            new("Nombre", typeof(string)), new("Lat", typeof(decimal)), new("Lng", typeof(decimal)), new("Proveedor", typeof(string)),
            new("CodigoExterno", typeof(string)), new("PrecioAcpm", typeof(decimal)), new("PrecioCorriente", typeof(decimal)),
            new("Producto", typeof(string)),
        ],
        [],
        [],
        [
            .. clsAsignacionMasiva.Directas("Nombre", "Lat", "Lng", "Proveedor", "CodigoExterno", "PrecioAcpm", "PrecioCorriente", "Producto"),
            new clsAsignacionMasiva("PrecioGalonActual", "COALESCE(s.[PrecioAcpm], s.[PrecioCorriente])"),
            new clsAsignacionMasiva("PrecioActualizadoAt", "SYSDATETIMEOFFSET()"),
            new clsAsignacionMasiva("SoportaPreautorizacion", "1"),
        ]);

    public async Task<IReadOnlyList<clsEstacionReferencia>> ListarPorProveedorAsync(string proveedor, CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirAsync(ct);
        var filas = await cn.QueryAsync<clsFilaReferencia>(new CommandDefinition(
            $"SELECT Id, Nombre, Lat, Lng, CodigoExterno FROM {Tabla} WHERE Proveedor = @proveedor",
            new { proveedor }, cancellationToken: ct));
        return filas.Select(f => new clsEstacionReferencia(f.Id, f.Nombre, (double?)f.Lat, (double?)f.Lng, f.CodigoExterno)).ToList();
    }

    public async Task<clsResultadoUpsertDto> ActualizarPreciosAsync(IReadOnlyList<clsActualizacionPrecioEstacionDto> precios, CancellationToken ct = default)
    {
        var filas = precios.Select(p => new object?[]
        {
            p.EstacionId, clsValoresMasivos.Decimal(p.Acpm, 2), clsValoresMasivos.Decimal(p.Corriente, 2),
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, EspecActualizarPrecios(), filas, f => f[0]!.ToString()!, ct);
    }

    public async Task<clsResultadoUpsertDto> InsertarAsync(IReadOnlyList<clsEstacionNuevaDto> estaciones, CancellationToken ct = default)
    {
        var filas = estaciones.Select(e => new object?[]
        {
            e.Nombre, clsValoresMasivos.Decimal(e.Lat, 7), clsValoresMasivos.Decimal(e.Lng, 7), e.Proveedor,
            clsValoresMasivos.Texto(e.CodigoExterno), clsValoresMasivos.Decimal(e.Acpm, 2), clsValoresMasivos.Decimal(e.Corriente, 2),
            clsValoresMasivos.Texto(e.Producto),
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, EspecInsertar(), filas, f => (string)f[0]!, ct);
    }

    public async Task<IReadOnlyList<clsEdsCandidata>> ListarConPrecioAcpmAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirAsync(ct);
        var filas = await cn.QueryAsync<clsFilaCandidata>(new CommandDefinition(
            $"SELECT Lat, Lng, PrecioAcpm FROM {Tabla} WHERE PrecioAcpm IS NOT NULL AND Lat IS NOT NULL AND Lng IS NOT NULL",
            cancellationToken: ct));
        return filas.Select(f => new clsEdsCandidata((double)f.Lat, (double)f.Lng, f.PrecioAcpm)).ToList();
    }

    public async Task<IReadOnlyList<Guid>> ObtenerInexistentesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return [];
        await using var cn = await conexion.AbrirAsync(ct);
        var existentes = (await cn.QueryAsync<Guid>(new CommandDefinition(
            $"SELECT Id FROM {Tabla} WHERE Id IN @ids", new { ids = ids.ToArray() }, cancellationToken: ct))).ToHashSet();
        return ids.Where(id => !existentes.Contains(id)).ToList();
    }

    public async Task<clsEstacionPrecioDto?> ActualizarPrecioGalonAsync(Guid estacionId, decimal precioGalon, CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirAsync(ct);
        // UPDATE y lectura en un lote: la tabla tiene trigger, asi que no se puede usar OUTPUT sin INTO.
        var fila = await cn.QuerySingleOrDefaultAsync<clsFilaEstacion>(new CommandDefinition(
            $"""
            UPDATE {Tabla} SET PrecioGalonActual = @precio, PrecioActualizadoAt = SYSDATETIMEOFFSET() WHERE Id = @id;
            SELECT Id, Nombre, Ruta, Lat, Lng, PrecioGalonActual, PrecioAcpm, PrecioCorriente, PrecioActualizadoAt,
                   SoportaPreautorizacion, Proveedor, Producto, CodigoExterno, CreatedAt, UpdatedAt
            FROM {Tabla} WHERE Id = @id;
            """,
            new { id = estacionId, precio = Math.Round(precioGalon, 2, MidpointRounding.AwayFromZero) }, cancellationToken: ct));
        return fila is null
            ? null
            : new clsEstacionPrecioDto(
                fila.Id, fila.Nombre, fila.Ruta, (double?)fila.Lat, (double?)fila.Lng, fila.PrecioGalonActual, fila.PrecioAcpm,
                fila.PrecioCorriente, fila.PrecioActualizadoAt, fila.SoportaPreautorizacion, fila.Proveedor, fila.Producto,
                fila.CodigoExterno, fila.CreatedAt, fila.UpdatedAt);
    }
}
