namespace Smartank.Application.Tests.Sincronizacion;

/// <summary>Reloj controlable para pruebas (no hace falta el paquete de testing de TimeProvider).</summary>
public sealed class clsRelojFalso(DateTimeOffset inicial) : TimeProvider
{
    private DateTimeOffset _ahora = inicial;

    public override DateTimeOffset GetUtcNow() => _ahora;

    public void Avanzar(TimeSpan intervalo) => _ahora = _ahora.Add(intervalo);
}
