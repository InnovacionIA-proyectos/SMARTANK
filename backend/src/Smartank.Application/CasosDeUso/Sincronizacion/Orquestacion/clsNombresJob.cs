namespace Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

/// <summary>
/// Nombres de los jobs de sincronizacion: son los valores de <c>--job &lt;nombre&gt;</c> del Worker (reemplazan a los
/// <c>npm run db:sync-*</c> del origen). "frecuente" y "diario" son grupos que encadenan varios jobs.
/// </summary>
public static class clsNombresJob
{
    public const string VEHICULOS = "vehiculos";
    public const string CONDUCTORES = "conductores";
    public const string VIAJES = "viajes";
    public const string RUTAS = "rutas";
    public const string RUTAS_INCREMENTAL = "rutas-incremental";
    public const string TANQUEOS = "tanqueos";
    public const string TELEMETRIA = "telemetria";
    public const string PRECIOS = "precios";
    public const string PRECIOS_RUTA = "precios-ruta";
    public const string RENDIMIENTO = "rendimiento";
    public const string FRECUENTE = "frecuente";
    public const string DIARIO = "diario";

    /// <summary>Jobs que se pueden ejecutar de a uno.</summary>
    public static readonly IReadOnlyList<string> Individuales =
        [VEHICULOS, CONDUCTORES, VIAJES, RUTAS, RUTAS_INCREMENTAL, TANQUEOS, TELEMETRIA, PRECIOS, PRECIOS_RUTA, RENDIMIENTO];

    /// <summary>Todos los nombres validos (individuales y grupos).</summary>
    public static readonly IReadOnlyList<string> Todos = [.. Individuales, FRECUENTE, DIARIO];

    /// <summary>Orden de dependencia del grupo diario: viajes antes de rutas (cruza los FO presentes); precios por ruta despues de rutas y precios.</summary>
    public static readonly IReadOnlyList<string> OrdenDiario =
        [VEHICULOS, CONDUCTORES, VIAJES, RUTAS, PRECIOS, PRECIOS_RUTA, RENDIMIENTO];

    public static bool EsValido(string? nombre) => nombre is not null && Todos.Contains(nombre, StringComparer.Ordinal);
}
