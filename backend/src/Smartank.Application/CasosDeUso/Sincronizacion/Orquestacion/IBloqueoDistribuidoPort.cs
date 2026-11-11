namespace Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

/// <summary>
/// Bloqueo entre replicas del Worker: dos instancias no deben correr el mismo grupo a la vez. El bloqueo dura mientras no se
/// libere el objeto devuelto (y se pierde solo si el proceso muere).
/// </summary>
public interface IBloqueoDistribuidoPort
{
    /// <summary>Intenta tomar el bloqueo sin esperar. Devuelve null si otra replica ya lo tiene.</summary>
    Task<IAsyncDisposable?> IntentarAdquirirAsync(string recurso, CancellationToken ct = default);
}
