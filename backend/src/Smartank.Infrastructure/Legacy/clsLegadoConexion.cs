using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Infrastructure.Legacy;

/// <summary>
/// Abre conexiones al SQL Server LEGADO (solo lectura): <c>ConnectionStrings__Fpsap</c> (flota, tanqueos y, con nombre de tres
/// partes, dbSolucionesTI) y <c>ConnectionStrings__Ebs</c> (DW de operaciones). Si la cadena falta se lanza
/// <c>*_NO_CONFIGURADO</c> al USARLA, nunca al arrancar. Si Ebs no define usuario y clave se reutilizan los de Fpsap (es el mismo
/// login en ambos servidores). Conexion con timeout de 15 s; las consultas de sincronizacion usan 120 s.
/// </summary>
public sealed class clsLegadoConexion(IConfiguration config)
{
    public const int TIMEOUT_CONEXION_SEGUNDOS = 15;
    public const int TIMEOUT_CONSULTA_SEGUNDOS = 120;

    public Task<SqlConnection> AbrirFpsapAsync(CancellationToken ct) => AbrirAsync(CadenaFpsap(), ct);

    public Task<SqlConnection> AbrirEbsAsync(CancellationToken ct) => AbrirAsync(CadenaEbs(), ct);

    /// <summary>Ejecuta una consulta de solo lectura con el timeout de sincronizacion y la devuelve como filas dinamicas (columnas tal cual en MAYUSCULAS).</summary>
    public static async Task<IReadOnlyList<clsFilaLegada>> ConsultarAsync(
        SqlConnection cn, string sql, object? parametros, CancellationToken ct)
    {
        var filas = await cn.QueryAsync(new CommandDefinition(
            sql, parametros, commandTimeout: TIMEOUT_CONSULTA_SEGUNDOS, cancellationToken: ct));
        return filas.Select(f => new clsFilaLegada((IDictionary<string, object?>)f)).ToList();
    }

    internal string CadenaFpsap()
    {
        var cadena = config.GetConnectionString("Fpsap");
        if (string.IsNullOrWhiteSpace(cadena))
            throw new clsServiceUnavailableException(
                "FPSAP_NO_CONFIGURADO",
                "El SQL Server legado (FPSAP) no esta configurado: defina la variable ConnectionStrings__Fpsap con la cadena de solo lectura.");
        return ConTimeout(new SqlConnectionStringBuilder(cadena)).ConnectionString;
    }

    internal string CadenaEbs()
    {
        var cadena = config.GetConnectionString("Ebs");
        if (string.IsNullOrWhiteSpace(cadena))
            throw new clsServiceUnavailableException(
                "EBS_NO_CONFIGURADO",
                "El DW de operaciones (EBS) no esta configurado: defina la variable ConnectionStrings__Ebs con la cadena de solo lectura.");

        var ebs = new SqlConnectionStringBuilder(cadena);
        var sinCredenciales = !ebs.IntegratedSecurity
            && (string.IsNullOrWhiteSpace(ebs.UserID) || string.IsNullOrWhiteSpace(ebs.Password));
        if (sinCredenciales && config.GetConnectionString("Fpsap") is { Length: > 0 } fpsapCadena)
        {
            var fpsap = new SqlConnectionStringBuilder(fpsapCadena);
            if (string.IsNullOrWhiteSpace(ebs.UserID)) ebs.UserID = fpsap.UserID;
            if (string.IsNullOrWhiteSpace(ebs.Password)) ebs.Password = fpsap.Password;
        }
        return ConTimeout(ebs).ConnectionString;
    }

    private static SqlConnectionStringBuilder ConTimeout(SqlConnectionStringBuilder b)
    {
        b.ConnectTimeout = TIMEOUT_CONEXION_SEGUNDOS;
        return b;
    }

    private static async Task<SqlConnection> AbrirAsync(string cadena, CancellationToken ct)
    {
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
}
