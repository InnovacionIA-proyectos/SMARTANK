namespace Smartank.Infrastructure.Persistence;

/// <summary>
/// Esquema de SQL Server donde viven las tablas de SMARTANK. Se lee de <c>Database__Esquema</c> directo del entorno
/// (sin IConfiguration) para que funcione en runtime, herramientas de EF y tests. Por defecto <c>dbo</c>, que es el
/// esquema que crea Database/ddl.sql; si se cambia, el ddl.sql debe ejecutarse con el mismo esquema.
/// </summary>
public static class clsEsquemaSmartank
{
    public const string ESQUEMA_POR_DEFECTO = "dbo";

    public static string Nombre =>
        Environment.GetEnvironmentVariable("Database__Esquema") is { Length: > 0 } valor ? valor : ESQUEMA_POR_DEFECTO;
}
