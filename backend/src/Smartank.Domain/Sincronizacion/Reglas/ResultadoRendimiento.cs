namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Resultado de derivar el rendimiento de un vehiculo desde los contadores de Geotab.</summary>
public enum ResultadoRendimiento
{
    Calculado,
    SinDatos,
    FueraDeBanda,
}
