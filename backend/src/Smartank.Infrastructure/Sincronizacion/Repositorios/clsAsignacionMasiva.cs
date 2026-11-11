namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>
/// Asignacion de una columna en el UPDATE/INSERT masivo. Sin <paramref name="Expresion"/> toma el valor de la tabla temporal
/// (<c>s.[Columna]</c>); con ella usa SQL fijo escrito en el codigo, que puede referirse a <c>s</c> (temporal) y <c>t</c> (destino),
/// por ejemplo <c>SYSDATETIMEOFFSET()</c> o <c>COALESCE(s.[X], t.[X])</c>. Nunca se arma con datos externos.
/// </summary>
public sealed record clsAsignacionMasiva(string Columna, string? Expresion = null)
{
    /// <summary>Asignaciones simples: cada columna toma el valor de la tabla temporal.</summary>
    public static IReadOnlyList<clsAsignacionMasiva> Directas(params string[] columnas) =>
        columnas.Select(c => new clsAsignacionMasiva(c)).ToList();
}
