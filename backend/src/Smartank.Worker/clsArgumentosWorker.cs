using Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

namespace Smartank.Worker;

/// <summary>Lee la linea de comandos del Worker: sin argumentos corre como servicio; <c>--job &lt;nombre&gt;</c> ejecuta UNA vez y sale.</summary>
public static class clsArgumentosWorker
{
    private const string BANDERA = "--job";

    /// <summary>
    /// Devuelve el nombre del job pedido (null = modo servicio). <paramref name="error"/> trae el mensaje si la bandera viene sin
    /// valor o el nombre no es uno de <see cref="clsNombresJob.Todos"/>.
    /// </summary>
    public static string? ObtenerJob(IReadOnlyList<string> args, out string? error)
    {
        error = null;
        for (var i = 0; i < args.Count; i++)
        {
            string? valor = null;
            if (string.Equals(args[i], BANDERA, StringComparison.Ordinal))
                valor = i + 1 < args.Count ? args[i + 1] : null;
            else if (args[i].StartsWith(BANDERA + "=", StringComparison.Ordinal))
                valor = args[i][(BANDERA.Length + 1)..];
            else
                continue;

            if (string.IsNullOrWhiteSpace(valor) || valor.StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Falta el nombre del job. Uso: --job <nombre>. Validos: {string.Join(", ", clsNombresJob.Todos)}.";
                return null;
            }
            if (!clsNombresJob.EsValido(valor))
            {
                error = $"Job '{valor}' desconocido. Validos: {string.Join(", ", clsNombresJob.Todos)}.";
                return null;
            }
            return valor;
        }
        return null;
    }
}
