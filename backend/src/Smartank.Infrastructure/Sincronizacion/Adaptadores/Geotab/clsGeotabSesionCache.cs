namespace Smartank.Infrastructure.Sincronizacion.Adaptadores.Geotab;

/// <summary>
/// Sesion de Geotab compartida por todo el proceso (singleton). El <see cref="SemaphoreSlim"/> hace que llamadas concurrentes
/// (la telemetria pide cuatro consultas en paralelo) autentiquen UNA sola vez, tambien al renovar una sesion vencida.
/// </summary>
public sealed class clsGeotabSesionCache
{
    private readonly SemaphoreSlim _candado = new(1, 1);
    private clsGeotabSesion? _sesion;

    /// <summary>
    /// Devuelve la sesion cacheada o autentica. Si se pasa <paramref name="invalida"/> (la sesion que acaba de fallar) y la
    /// cache ya tiene otra distinta, otro hilo ya la renovo y se reutiliza; si no, se vuelve a autenticar.
    /// </summary>
    public async Task<clsGeotabSesion> ObtenerAsync(
        Func<CancellationToken, Task<clsGeotabSesion>> autenticar, clsGeotabSesion? invalida, CancellationToken ct)
    {
        await _candado.WaitAsync(ct);
        try
        {
            if (_sesion is not null && !ReferenceEquals(_sesion, invalida)) return _sesion;
            _sesion = await autenticar(ct);
            return _sesion;
        }
        finally
        {
            _candado.Release();
        }
    }

    /// <summary>Olvida la sesion (la proxima llamada autentica).</summary>
    public void Invalidar() => _sesion = null;
}
