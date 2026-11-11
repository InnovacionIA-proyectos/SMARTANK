using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;
using Smartank.Infrastructure.Sincronizacion.Repositorios;

namespace Smartank.Infrastructure.Sincronizacion.Adaptadores;

/// <summary>
/// Bloqueo entre replicas con el bloqueo de aplicacion de SQL Server (<c>sp_getapplock</c>, modo Exclusive, dueno Session) sobre
/// la base propia. Usa una conexion DEDICADA que se mantiene abierta mientras corre el grupo: el bloqueo vive lo que viva esa
/// sesion, de modo que si el proceso muere el servidor lo libera solo y ninguna replica queda bloqueada para siempre.
/// </summary>
public sealed class clsBloqueoDistribuidoSqlServer(clsConexionSmartank conexion) : IBloqueoDistribuidoPort
{
    private const int TIMEOUT_COMANDO_SEGUNDOS = 15;

    public async Task<IAsyncDisposable?> IntentarAdquirirAsync(string recurso, CancellationToken ct = default)
    {
        var cn = await conexion.AbrirAsync(ct);
        try
        {
            var parametros = new DynamicParameters();
            parametros.Add("Resource", recurso, DbType.String, size: 255);
            parametros.Add("LockMode", "Exclusive", DbType.String, size: 32);
            parametros.Add("LockOwner", "Session", DbType.String, size: 32);
            parametros.Add("LockTimeout", 0, DbType.Int32);          // no esperar: si otra replica corre el grupo, se omite
            parametros.Add("Resultado", dbType: DbType.Int32, direction: ParameterDirection.ReturnValue);

            await cn.ExecuteAsync(new CommandDefinition(
                "sp_getapplock", parametros, commandType: CommandType.StoredProcedure,
                commandTimeout: TIMEOUT_COMANDO_SEGUNDOS, cancellationToken: ct));

            // 0 = concedido; 1 = concedido tras esperar; negativos = no concedido (-1 tiempo agotado, -2 cancelado, -3 interbloqueo, -999 error).
            if (parametros.Get<int>("Resultado") < 0)
            {
                await cn.DisposeAsync();
                return null;
            }
            return new clsCandado(cn, recurso);
        }
        catch
        {
            await cn.DisposeAsync();
            throw;
        }
    }

    private sealed class clsCandado(SqlConnection cn, string recurso) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                // Liberacion explicita: la conexion vuelve al pool y el reinicio del pool no garantiza soltar el bloqueo de sesion.
                var parametros = new DynamicParameters();
                parametros.Add("Resource", recurso, DbType.String, size: 255);
                parametros.Add("LockOwner", "Session", DbType.String, size: 32);
                await cn.ExecuteAsync(new CommandDefinition(
                    "sp_releaseapplock", parametros, commandType: CommandType.StoredProcedure, commandTimeout: TIMEOUT_COMANDO_SEGUNDOS));
            }
            catch (Exception)
            {
                // Si la liberacion falla la conexion esta rota y el servidor ya solto el bloqueo al cerrar la sesion.
            }
            finally
            {
                await cn.DisposeAsync();
            }
        }
    }
}
