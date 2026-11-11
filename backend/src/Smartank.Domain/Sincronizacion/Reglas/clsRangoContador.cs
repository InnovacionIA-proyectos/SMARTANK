namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>
/// Valor del contador acumulado de un dispositivo al INICIO y al FINAL de la ventana (la lectura mas antigua y la mas
/// reciente por fecha, no el minimo y maximo numericos: un reinicio del contador debe verse como delta negativo).
/// </summary>
public sealed class clsRangoContador
{
    private long _inicioMs;
    private long _finMs;

    public double Inicial { get; private set; }

    public double Final { get; private set; }

    public int Lecturas { get; private set; }

    /// <summary>Incorpora una lectura; ante fechas iguales conserva la primera que llego.</summary>
    public void Agregar(double valor, DateTimeOffset fecha)
    {
        var ms = fecha.ToUnixTimeMilliseconds();
        if (Lecturas == 0)
        {
            Inicial = Final = valor;
            _inicioMs = _finMs = ms;
        }
        else
        {
            if (ms < _inicioMs)
            {
                _inicioMs = ms;
                Inicial = valor;
            }
            if (ms > _finMs)
            {
                _finMs = ms;
                Final = valor;
            }
        }
        Lecturas++;
    }
}
