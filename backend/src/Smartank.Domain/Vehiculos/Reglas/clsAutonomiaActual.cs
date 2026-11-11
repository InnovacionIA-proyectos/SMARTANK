namespace Smartank.Domain.Vehiculos.Reglas;

/// <summary>
/// Autonomia ACTUAL de un vehiculo (km que puede recorrer con el combustible que tiene hoy en el tanque).
/// Distinta de la autonomia estimada al despachar un viaje, que se guarda en el propio viaje.
/// </summary>
public static class clsAutonomiaActual
{
    /// <summary>
    /// autonomia = nivel x rendimiento x (1 - tolerancia), redondeada al km entero (mitades hacia arriba, igual que el
    /// round de la consulta original). El rendimiento es el historico; si no hay, el esperado; si tampoco, el default
    /// de negocio (el maestro de flota no lo trae). Devuelve null si el vehiculo no tiene lectura de nivel.
    /// </summary>
    public static double? Calcular(
        decimal? nivelGalones,
        double? rendimientoHistorico,
        double? rendimientoEsperado,
        double rendimientoDefault,
        double toleranciaMotor)
    {
        if (nivelGalones is null) return null;

        var rendimiento = rendimientoHistorico ?? rendimientoEsperado ?? rendimientoDefault;
        var autonomia = (double)nivelGalones.Value * rendimiento * (1 - toleranciaMotor);
        return Math.Round(autonomia, 0, MidpointRounding.AwayFromZero);
    }
}
