using System.Text.RegularExpressions;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Application.CasosDeUso.Metricas.Interfaces;
using Smartank.Domain.Common;
using Smartank.Domain.Metricas.Reglas;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Metricas.Repositorios;

/// <summary>
/// Consultas del tablero de Finanzas sobre la base propia. La que liga cada tanqueo con su viaje, su ruta, su referencia
/// de precio y su estacion usa SQL (Dapper) porque son cuatro correlaciones (OUTER APPLY TOP 1) que EF no expresa con
/// claridad; las agregaciones (las formulas) NO viven aqui sino en <see cref="clsCalculoRecuperacion"/>.
/// </summary>
public sealed partial class clsSqlMetricasRepository(clsSmartankDbContext db) : IMetricasRepository
{
    /// <summary>Combustible que cuenta para la recuperacion de margen; la comparacion no distingue mayusculas (collation de la base).</summary>
    private const string PRODUCTO_DIESEL = "diesel";

    /// <summary>Dias hacia atras en que se busca el viaje de un tanqueo: el viaje vigente es el iniciado hasta 30 dias antes.</summary>
    private const int VENTANA_VIAJE_DIAS = 30;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]{0,127}$")]
    private static partial Regex PatronIdentificador();

    public async Task<IReadOnlyList<clsTanqueoMetrica>> ListarTanqueosAsync(clsRecuperacionFiltrosDto filtros, CancellationToken ct)
    {
        var (sql, parametros) = ConstruirConsulta(filtros);

        // EF administra la conexion (la abre y la cierra); Dapper solo ejecuta sobre ella.
        await db.Database.OpenConnectionAsync(ct);
        try
        {
            // Si el llamador abrio una transaccion sobre el DbContext, Dapper debe ejecutar dentro de ella.
            var transaccion = db.Database.CurrentTransaction?.GetDbTransaction();
            var filas = await db.Database.GetDbConnection().QueryAsync<clsFilaTanqueoSql>(
                new CommandDefinition(sql, parametros, transaccion, cancellationToken: ct));
            return filas.Select(clsFilaTanqueoSql.ADominio).ToList();
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    public async Task<clsOpcionesFiltroDto> ObtenerOpcionesAsync(CancellationToken ct)
    {
        // Secuencial a proposito: un DbContext no admite consultas concurrentes.
        var programas = await db.Vehiculos.AsNoTracking()
            .Where(v => v.Programa != null).Select(v => v.Programa!).Distinct().OrderBy(p => p).ToListAsync(ct);

        var proveedores = (await db.Tanqueos.AsNoTracking().Select(t => t.Fuente).Distinct().ToListAsync(ct))
            .Select(clsSnakeCase.ToSnake).OrderBy(p => p, StringComparer.Ordinal).ToList();

        var diesel = db.Tanqueos.AsNoTracking().Where(t => t.Producto == PRODUCTO_DIESEL);
        var productos = await diesel.Select(t => t.Producto!).Distinct().OrderBy(p => p).ToListAsync(ct);
        var anios = await diesel.Select(t => t.Fecha.Year).Distinct().OrderByDescending(a => a).ToListAsync(ct);
        var meses = await diesel.Select(t => t.Fecha.Month).Distinct().OrderBy(m => m).ToListAsync(ct);

        // El mismo criterio de nombre de ruta que usa la consulta de tanqueos (nombre legible, codigo o ruta).
        var rutas = await db.Viajes.AsNoTracking()
            .Select(v => v.NombreRuta ?? v.CodigoRuta ?? v.Ruta).Distinct().OrderBy(r => r).ToListAsync(ct);

        return new clsOpcionesFiltroDto(programas, proveedores, productos, anios, meses, rutas);
    }

    /// <summary>SQL con parametros nombrados: ningun filtro se concatena, y el esquema se valida como identificador.</summary>
    internal static (string Sql, DynamicParameters Parametros) ConstruirConsulta(clsRecuperacionFiltrosDto f)
    {
        var esquema = clsEsquemaSmartank.Nombre;
        if (!PatronIdentificador().IsMatch(esquema))
            throw new InvalidOperationException("Database__Esquema no es un identificador de esquema válido.");
        var s = $"[{esquema}]";

        var parametros = new DynamicParameters();
        parametros.Add("producto", PRODUCTO_DIESEL);
        parametros.Add("ventanaDias", -VENTANA_VIAJE_DIAS);

        var condiciones = new List<string> { "t.Producto = @producto" };
        if (f.Programa is not null) { condiciones.Add("ve.Programa = @programa"); parametros.Add("programa", f.Programa); }
        if (f.Proveedor is not null) { condiciones.Add("t.Fuente = @proveedor"); parametros.Add("proveedor", f.Proveedor); }
        if (f.Producto is not null) { condiciones.Add("t.Producto = @productoFiltro"); parametros.Add("productoFiltro", f.Producto); }
        if (f.Anio is not null) { condiciones.Add("YEAR(t.Fecha) = @anio"); parametros.Add("anio", f.Anio); }
        if (f.Mes is not null) { condiciones.Add("MONTH(t.Fecha) = @mes"); parametros.Add("mes", f.Mes); }
        if (f.Ruta is not null) { condiciones.Add("vj.Ruta = @ruta"); parametros.Add("ruta", f.Ruta); }

        var sql = $"""
            SELECT t.Id, t.Fuente AS Proveedor, t.Placa, t.Fecha,
                   t.Galones, t.ValorGalon AS PrecioPagado, t.ValorTotal,
                   t.Estacion, t.Ciudad,
                   vj.Ruta, pr.AcpmMin, pr.AcpmMax,
                   em.Id AS EstId, em.Nombre AS EstNombre, em.Lat AS EstLat, em.Lng AS EstLng,
                   em.Proveedor AS EstProveedor, em.PrecioAcpm AS EstPrecio,
                   rt.OrigenLat, rt.OrigenLng, rt.DestinoLat, rt.DestinoLng
            FROM {s}.[tblTanqueos] t
            LEFT JOIN {s}.[tblVehiculos] ve ON ve.Placa = t.Placa
            OUTER APPLY (
                SELECT TOP (1) v.Id AS ViajeId, COALESCE(v.NombreRuta, v.CodigoRuta, v.Ruta) AS Ruta
                FROM {s}.[tblViajes] v
                WHERE v.VehiculoId = ve.Id
                  AND v.FechaEfectiva <= t.Fecha
                  AND v.FechaEfectiva >= DATEADD(day, @ventanaDias, t.Fecha)
                ORDER BY v.FechaEfectiva DESC
            ) vj
            LEFT JOIN {s}.[tblViajePreciosRef] pr ON pr.ViajeId = vj.ViajeId
            OUTER APPLY (
                SELECT TOP (1) r.OrigenLat, r.OrigenLng, r.DestinoLat, r.DestinoLng
                FROM {s}.[tblRutas] r
                WHERE r.ViajeId = vj.ViajeId
                  AND r.OrigenLat IS NOT NULL AND r.OrigenLng IS NOT NULL
                  AND r.DestinoLat IS NOT NULL AND r.DestinoLng IS NOT NULL
                ORDER BY r.FechaCalculo DESC
            ) rt
            OUTER APPLY (
                SELECT TOP (1) e.Id, e.Nombre, e.Lat, e.Lng, e.Proveedor, e.PrecioAcpm
                FROM {s}.[tblEstaciones] e
                WHERE e.Lat IS NOT NULL AND e.Lng IS NOT NULL
                  AND (e.NombreNormalizado = LOWER(LTRIM(RTRIM(t.Estacion)))
                       -- El historial Distracom trae "<NOMBRE>"; el catalogo, "DISTRACOM <NOMBRE>".
                       OR e.NombreNormalizado = N'distracom ' + LOWER(LTRIM(RTRIM(t.Estacion))))
                ORDER BY e.Id
            ) em
            WHERE {string.Join(" AND ", condiciones)}
            ORDER BY t.Fecha, t.Id
            """;

        return (sql, parametros);
    }

    /// <summary>Fila tal como la entrega SQL Server (decimal para las coordenadas); se convierte al registro de dominio.</summary>
    private sealed class clsFilaTanqueoSql
    {
        public Guid Id { get; set; }
        public string? Proveedor { get; set; }
        public string Placa { get; set; } = string.Empty;
        public DateTimeOffset Fecha { get; set; }
        public decimal? Galones { get; set; }
        public decimal? PrecioPagado { get; set; }
        public decimal? ValorTotal { get; set; }
        public string? Estacion { get; set; }
        public string? Ciudad { get; set; }
        public string? Ruta { get; set; }
        public decimal? AcpmMin { get; set; }
        public decimal? AcpmMax { get; set; }
        public Guid? EstId { get; set; }
        public string? EstNombre { get; set; }
        public decimal? EstLat { get; set; }
        public decimal? EstLng { get; set; }
        public string? EstProveedor { get; set; }
        public decimal? EstPrecio { get; set; }
        public decimal? OrigenLat { get; set; }
        public decimal? OrigenLng { get; set; }
        public decimal? DestinoLat { get; set; }
        public decimal? DestinoLng { get; set; }

        public static clsTanqueoMetrica ADominio(clsFilaTanqueoSql f) => new(
            f.Id, f.Proveedor, f.Placa, f.Fecha, f.Galones, f.PrecioPagado, f.ValorTotal, f.Estacion, f.Ciudad, f.Ruta,
            f.AcpmMin, f.AcpmMax,
            f.EstId is { } id && f.EstLat is { } lat && f.EstLng is { } lng
                ? new clsEstacionCatalogo(id, f.EstNombre ?? string.Empty, (double)lat, (double)lng, f.EstProveedor, f.EstPrecio)
                : null,
            f.OrigenLat is { } oLat && f.OrigenLng is { } oLng && f.DestinoLat is { } dLat && f.DestinoLng is { } dLng
                ? new clsExtremosRuta((double)oLat, (double)oLng, (double)dLat, (double)dLng)
                : null);
    }
}
