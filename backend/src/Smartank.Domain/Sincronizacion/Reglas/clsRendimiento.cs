namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>
/// Rendimiento real (km/galon) de un vehiculo a partir de los contadores acumulados de Geotab:
/// rendimiento = delta odometro (km) / (delta combustible total (L) / 3.785411784).
/// </summary>
public static class clsRendimiento
{
    public const double LITROS_POR_GALON = 3.785411784;

    // Minimos para un promedio estable y banda plausible (km/galon): fuera de ella el dato es ruido del contador.
    private const double MIN_GALONES = 30;
    private const double MIN_KM = 100;
    private const double KM_GAL_MIN = 1;
    private const double KM_GAL_MAX = 30;

    /// <summary>Calcula el rendimiento; los valores iniciales/finales son las lecturas del contador al inicio y al final de la ventana.</summary>
    public static clsRendimientoCalculado Calcular(
        double combustibleInicialLitros, double combustibleFinalLitros, double odometroInicialMetros, double odometroFinalMetros)
    {
        var galones = (combustibleFinalLitros - combustibleInicialLitros) / LITROS_POR_GALON;
        var km = (odometroFinalMetros - odometroInicialMetros) / 1000.0;
        if (galones < MIN_GALONES || km < MIN_KM) return new clsRendimientoCalculado(ResultadoRendimiento.SinDatos, null);

        var kmPorGalon = km / galones;
        if (kmPorGalon < KM_GAL_MIN || kmPorGalon > KM_GAL_MAX)
            return new clsRendimientoCalculado(ResultadoRendimiento.FueraDeBanda, null);

        return new clsRendimientoCalculado(ResultadoRendimiento.Calculado, Math.Round(kmPorGalon, 2, MidpointRounding.AwayFromZero));
    }
}
