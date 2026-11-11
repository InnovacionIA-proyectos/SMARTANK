namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Conversion del nivel de tanque y del odometro que reporta Geotab.</summary>
public static class clsNivelCombustible
{
    /// <summary>
    /// Geotab entrega el nivel en porcentaje (DiagnosticFuelLevelId) y la base lo guarda en galones:
    /// galones = (porcentaje / 100) x capacidad del tanque, redondeado a 2 decimales (mitad hacia arriba, como el origen).
    /// </summary>
    public static decimal Galones(double porcentaje, decimal capacidadTanqueGal) =>
        (decimal)(Math.Round(porcentaje / 100.0 * (double)capacidadTanqueGal * 100.0, 0, MidpointRounding.AwayFromZero) / 100.0);

    /// <summary>El odometro de Geotab viene en metros; la base lo guarda en km.</summary>
    public static double OdometroKm(double metros) => metros / 1000.0;
}
