using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>
/// Conexion directa a la base propia (<c>ConnectionStrings__Smartank</c>) para las escrituras masivas de las sincronizaciones
/// (tabla temporal + SqlBulkCopy), que EF Core no hace de forma eficiente. No usa el DbContext, asi que no aplica su estrategia
/// de reintentos: cada corrida es idempotente y el siguiente ciclo reintenta.
/// </summary>
public sealed class clsConexionSmartank(IConfiguration config)
{
    public async Task<SqlConnection> AbrirAsync(CancellationToken ct)
    {
        var cadena = config.GetConnectionString("Smartank");
        if (string.IsNullOrWhiteSpace(cadena))
            throw new InvalidOperationException("Falta la cadena de conexion de la base propia (ConnectionStrings__Smartank).");

        var cn = new SqlConnection(cadena);
        try
        {
            await cn.OpenAsync(ct);
            return cn;
        }
        catch
        {
            await cn.DisposeAsync();
            throw;
        }
    }

    /// <summary>Nombre calificado y entre corchetes de una tabla propia, con el esquema configurado (<c>Database__Esquema</c>).</summary>
    public static string Tabla(string nombre) => $"[{Escapar(clsEsquemaSmartank.Nombre)}].[{Escapar(nombre)}]";

    private static string Escapar(string identificador) => identificador.Replace("]", "]]", StringComparison.Ordinal);
}
