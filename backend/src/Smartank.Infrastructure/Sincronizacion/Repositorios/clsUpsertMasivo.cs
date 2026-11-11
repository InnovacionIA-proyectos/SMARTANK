using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>
/// Upsert masivo por conjuntos sobre una tabla propia, sin MERGE: las filas se cargan con SqlBulkCopy a una tabla temporal
/// (creada con <c>SELECT TOP 0 ... INTO</c> para heredar tipos y colaciones de la tabla real) y luego un UPDATE por conjunto
/// (filas existentes) y un INSERT por conjunto (las que faltan) corren dentro de una transaccion. Un trigger de la tabla
/// refresca UpdatedAt; este helper nunca asigna UpdatedAt ni CreatedAt.
/// <para>
/// Aislamiento de errores: si el lote entero falla por un DATO (unico, nulo, check, FK, truncamiento, conversion), se parte por la
/// mitad y se reintenta hasta aislar las filas culpables, que se reportan sin abortar el resto. Otro tipo de fallo (conexion,
/// permisos, esquema) se propaga: no es culpa de una fila.
/// </para>
/// </summary>
public static class clsUpsertMasivo
{
    private const int TIMEOUT_SEGUNDOS = 120;
    private const int FILAS_POR_LOTE_BULK = 5000;
    private const int LARGO_MAXIMO_MENSAJE = 300;

    // Violacion de unico (2627, 2601), NULL en NOT NULL (515), CHECK/FK (547), truncamiento (8152) y conversion/desborde (245, 220, 8114, 8115, 242, 241).
    private static readonly HashSet<int> NUMEROS_ERROR_DE_DATOS = [2627, 2601, 515, 547, 8152, 245, 220, 8114, 8115, 242, 241];

    /// <param name="filas">Cada fila es un arreglo de valores en el MISMO orden que <see cref="clsEspecMasiva.Columnas"/> (null = NULL).</param>
    /// <param name="claveError">Texto que identifica la fila en el detalle de errores (placa, FO, cedula...).</param>
    public static async Task<clsResultadoUpsertDto> EjecutarAsync(
        SqlConnection cn, clsEspecMasiva spec, IReadOnlyList<object?[]> filas, Func<object?[], string> claveError, CancellationToken ct)
    {
        if (filas.Count == 0) return clsResultadoUpsertDto.Vacio;

        var indicesClave = spec.Clave.Select(c => IndiceDe(spec, c)).ToArray();
        var unicas = indicesClave.Length == 0 ? filas.ToArray() : Deduplicar(filas, indicesClave);

        var insertados = 0;
        var actualizados = 0;
        var errores = new List<clsErrorSincronizacion>();

        async Task ProcesarAsync(object?[][] lote)
        {
            try
            {
                var (actualizadasLote, insertadasLote) = await EjecutarLoteAsync(cn, spec, lote, ct);
                actualizados += actualizadasLote;
                insertados += insertadasLote;
            }
            catch (Exception ex) when (EsErrorDeDatos(ex) && cn.State == ConnectionState.Open && !ct.IsCancellationRequested)
            {
                if (lote.Length == 1)
                {
                    errores.Add(new clsErrorSincronizacion(claveError(lote[0]), Acortar(ex.Message)));
                    return;
                }
                var mitad = lote.Length / 2;
                await ProcesarAsync(lote[..mitad]);
                await ProcesarAsync(lote[mitad..]);
            }
        }

        await ProcesarAsync(unicas);
        return new clsResultadoUpsertDto(insertados, actualizados, errores);
    }

    private static async Task<(int Actualizadas, int Insertadas)> EjecutarLoteAsync(
        SqlConnection cn, clsEspecMasiva spec, object?[][] lote, CancellationToken ct)
    {
        using var tabla = ConstruirTabla(spec, lote);
        var columnas = string.Join(", ", spec.Columnas.Select(c => Id(c.Nombre)));
        var cruce = string.Join(" AND ", spec.Clave.Select(c => $"t.{Id(c)} = s.{Id(c)}"));

        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);
        try
        {
            await EjecutarAsync(cn, tx,
                $"IF OBJECT_ID('tempdb..#stg') IS NOT NULL DROP TABLE #stg; SELECT TOP (0) {columnas} INTO #stg FROM {spec.Tabla};", ct);

            using (var bulk = new SqlBulkCopy(cn, SqlBulkCopyOptions.Default, tx)
            {
                DestinationTableName = "#stg",
                BulkCopyTimeout = TIMEOUT_SEGUNDOS,
                BatchSize = FILAS_POR_LOTE_BULK,
            })
            {
                foreach (var c in spec.Columnas) bulk.ColumnMappings.Add(c.Nombre, c.Nombre);
                await bulk.WriteToServerAsync(tabla, ct);
            }

            var actualizadas = 0;
            if (spec.Actualizar.Count > 0 && spec.Clave.Count > 0)
            {
                var asignaciones = string.Join(", ", spec.Actualizar.Select(a => $"t.{Id(a.Columna)} = {a.Expresion ?? "s." + Id(a.Columna)}"));
                actualizadas = await EjecutarAsync(cn, tx,
                    $"UPDATE t SET {asignaciones} FROM {spec.Tabla} AS t INNER JOIN #stg AS s ON {cruce};", ct);
            }

            var insertadas = 0;
            if (spec.Insertar.Count > 0)
            {
                var destino = string.Join(", ", spec.Insertar.Select(a => Id(a.Columna)));
                var origen = string.Join(", ", spec.Insertar.Select(a => a.Expresion ?? "s." + Id(a.Columna)));
                var filtro = spec.Clave.Count == 0 ? string.Empty : $" WHERE NOT EXISTS (SELECT 1 FROM {spec.Tabla} AS t WHERE {cruce})";
                insertadas = await EjecutarAsync(cn, tx,
                    $"INSERT INTO {spec.Tabla} ({destino}) SELECT {origen} FROM #stg AS s{filtro};", ct);
            }

            await tx.CommitAsync(ct);
            return (actualizadas, insertadas);
        }
        catch
        {
            try { await tx.RollbackAsync(CancellationToken.None); }
            catch (Exception) { /* la conexion ya cayo: el error original es el que importa */ }
            throw;
        }
    }

    private static async Task<int> EjecutarAsync(SqlConnection cn, SqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(sql, cn, tx) { CommandTimeout = TIMEOUT_SEGUNDOS };
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static DataTable ConstruirTabla(clsEspecMasiva spec, object?[][] lote)
    {
        var tabla = new DataTable();
        foreach (var c in spec.Columnas) tabla.Columns.Add(c.Nombre, c.Tipo);
        tabla.BeginLoadData();
        foreach (var fila in lote)
        {
            var valores = new object[fila.Length];
            for (var i = 0; i < fila.Length; i++) valores[i] = fila[i] ?? DBNull.Value;
            tabla.Rows.Add(valores);
        }
        tabla.EndLoadData();
        return tabla;
    }

    /// <summary>Si una llave se repite en la misma carga gana la ultima fila (igual que dos upserts sucesivos) y se conserva su orden.</summary>
    private static object?[][] Deduplicar(IReadOnlyList<object?[]> filas, int[] indicesClave)
    {
        var porClave = new Dictionary<string, int>(StringComparer.Ordinal);
        var resultado = new List<object?[]>(filas.Count);
        foreach (var fila in filas)
        {
            var clave = string.Join('\u001f', indicesClave.Select(i =>
                Convert.ToString(fila[i], CultureInfo.InvariantCulture)?.ToUpperInvariant() ?? string.Empty));
            if (porClave.TryGetValue(clave, out var posicion))
            {
                resultado[posicion] = fila;
            }
            else
            {
                porClave[clave] = resultado.Count;
                resultado.Add(fila);
            }
        }
        return [.. resultado];
    }

    private static bool EsErrorDeDatos(Exception ex) => ex switch
    {
        SqlException sql => sql.Errors.Cast<SqlError>().Any(e => NUMEROS_ERROR_DE_DATOS.Contains(e.Number)),
        // SqlBulkCopy reporta truncamiento y conversion de tipos como InvalidOperationException/InvalidCast/Format/Overflow.
        InvalidOperationException or InvalidCastException or FormatException or OverflowException => true,
        _ => false,
    };

    private static int IndiceDe(clsEspecMasiva spec, string columna)
    {
        for (var i = 0; i < spec.Columnas.Count; i++)
            if (string.Equals(spec.Columnas[i].Nombre, columna, StringComparison.OrdinalIgnoreCase)) return i;
        throw new ArgumentException($"La columna clave '{columna}' no esta entre las columnas cargadas de {spec.Tabla}.");
    }

    private static string Id(string identificador) => $"[{identificador.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string Acortar(string mensaje)
    {
        var linea = mensaje.ReplaceLineEndings(" ");
        return linea.Length <= LARGO_MAXIMO_MENSAJE ? linea : linea[..LARGO_MAXIMO_MENSAJE] + "...";
    }
}
