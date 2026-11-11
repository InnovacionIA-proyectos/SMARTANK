namespace Smartank.Domain.Eventos.Reglas;

/// <summary>Reglas del registro de la bitacora del conductor.</summary>
public static class clsReglasBitacora
{
    /// <summary>
    /// True si <paramref name="ahora"/> esta dentro de la ventana de la autorizacion (inicio y fin inclusivos): el conductor solo
    /// puede reportar una carga mientras la orden esta vigente.
    /// </summary>
    public static bool EstaDentroDeVentana(DateTimeOffset fechaInicio, DateTimeOffset fechaFin, DateTimeOffset ahora) =>
        ahora >= fechaInicio && ahora <= fechaFin;

    /// <summary>
    /// Combustible cargado a registrar en el evento: se deriva del tipo de motor del vehiculo (diesel, gasolina...), igual
    /// que el origen. Null si el tipo de motor esta vacio.
    /// </summary>
    public static string? DerivarProducto(string? tipoMotor) =>
        string.IsNullOrWhiteSpace(tipoMotor) ? null : tipoMotor.Trim();
}
