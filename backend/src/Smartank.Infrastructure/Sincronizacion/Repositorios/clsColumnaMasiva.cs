namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>Columna real de la tabla destino que viaja por la tabla temporal (nombre y tipo CLR del valor en el DataTable).</summary>
public sealed record clsColumnaMasiva(string Nombre, Type Tipo);
