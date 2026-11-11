namespace Smartank.Domain.Autorizaciones.Reglas;

/// <summary>De donde sale el rendimiento (km/galon) usado para calcular la autonomia. En JSON: historico | esperado | default.</summary>
public enum BaseRendimiento
{
    Historico,
    Esperado,
    Default,
}

/// <summary>Rendimiento efectivo (km por galon) y la base de la que se tomo.</summary>
public readonly record struct clsRendimientoResuelto(double KmPorGalon, BaseRendimiento Base);

/// <summary>
/// Regla de autonomia: <c>autonomia_km = nivel x rendimiento x (1 - tolerancia)</c>. La tolerancia corrige hacia abajo para
/// no prometer mas kilometros de los que el tanque seguramente da. Reglas puras, sin acceso a datos.
/// </summary>
public static class clsAutonomia
{
    /// <summary>Autonomia en km. Lanza <see cref="ArgumentOutOfRangeException"/> si el nivel o el rendimiento son negativos o la tolerancia sale de [0, 1].</summary>
    public static double CalcularKm(double nivelGalones, double rendimientoKmPorGalon, double tolerancia)
    {
        if (nivelGalones < 0 || rendimientoKmPorGalon < 0)
            throw new ArgumentOutOfRangeException(nameof(nivelGalones), "El nivel y el rendimiento deben ser no negativos.");
        if (tolerancia is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(tolerancia), "La tolerancia debe estar entre 0 y 1.");

        return nivelGalones * rendimientoKmPorGalon * (1 - tolerancia);
    }

    /// <summary>
    /// Rendimiento a usar: el historico del vehiculo; si no hay, el esperado (teorico); si tampoco, el valor por defecto del
    /// negocio (el maestro de flota no lo trae). Un 0 explicito cuenta como dato, igual que en el origen.
    /// </summary>
    public static clsRendimientoResuelto ResolverRendimiento(double? historico, double? esperado, double porDefecto)
    {
        if (historico is { } h) return new clsRendimientoResuelto(h, BaseRendimiento.Historico);
        if (esperado is { } e) return new clsRendimientoResuelto(e, BaseRendimiento.Esperado);
        return new clsRendimientoResuelto(porDefecto, BaseRendimiento.Default);
    }

    /// <summary>
    /// True si la lectura de nivel es mas vieja que <paramref name="maxHoras"/>. Una lectura sin fecha no se puede juzgar
    /// y no se considera obsoleta (los datos cargados a mano no traen fecha de lectura).
    /// </summary>
    public static bool EsLecturaObsoleta(DateTimeOffset? lecturaAt, DateTimeOffset ahora, double maxHoras)
    {
        if (lecturaAt is not { } lectura) return false;
        return ahora - lectura > TimeSpan.FromHours(maxHoras);
    }

    /// <summary>Redondeo comercial (mitad hacia arriba, como Math.round de JavaScript en positivos) a <paramref name="decimales"/> decimales.</summary>
    public static double Redondear(double valor, int decimales) =>
        Math.Round(valor, decimales, MidpointRounding.AwayFromZero);
}
