namespace Smartank.Application.Tests.Autorizaciones;

/// <summary>Reloj de pruebas con la hora fija (y avanzable), para probar ventanas y vigencias sin depender del reloj real.</summary>
public sealed class clsRelojFijo(DateTimeOffset ahora) : TimeProvider
{
    private DateTimeOffset _ahora = ahora;

    public override DateTimeOffset GetUtcNow() => _ahora;

    public void Avanzar(TimeSpan tiempo) => _ahora += tiempo;
}
