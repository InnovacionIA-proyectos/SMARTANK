namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>
/// Describe una escritura masiva sobre una tabla propia.
/// <list type="bullet">
/// <item><see cref="Columnas"/>: columnas reales (sin calculadas ni la llave autogenerada) que se cargan a la tabla temporal.</item>
/// <item><see cref="Clave"/>: columnas de cruce con la tabla destino (vacio = solo insertar, sin comprobar existencia).</item>
/// <item><see cref="Actualizar"/>: asignaciones del UPDATE de las filas que ya existen (vacio = no actualiza).</item>
/// <item><see cref="Insertar"/>: asignaciones del INSERT de las que no existen (vacio = no inserta).</item>
/// </list>
/// </summary>
public sealed record clsEspecMasiva(
    string Tabla,
    IReadOnlyList<clsColumnaMasiva> Columnas,
    IReadOnlyList<string> Clave,
    IReadOnlyList<clsAsignacionMasiva> Actualizar,
    IReadOnlyList<clsAsignacionMasiva> Insertar);
