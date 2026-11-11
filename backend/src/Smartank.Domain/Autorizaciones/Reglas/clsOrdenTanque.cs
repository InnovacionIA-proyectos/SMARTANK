using System.Globalization;

namespace Smartank.Domain.Autorizaciones.Reglas;

/// <summary>Datos de la orden de tanqueo que se le envia al conductor.</summary>
public sealed record clsDatosOrdenTanqueo(
    string Estacion,
    decimal GalonesAutorizados,
    string? Placa,
    string? Manifiesto,
    string? Ruta,
    string? MapaUrl,
    string? BitacoraUrl);

/// <summary>Arma el texto de la orden de tanqueo (el mismo formato que se enviaba en el origen).</summary>
public static class clsOrdenTanque
{
    // Camion de reparto (U+1F69A). Se escribe con escape para no depender de la codificacion del archivo fuente.
    private const string ICONO_CAMION = "\U0001F69A";

    /// <summary>Texto en lineas; las lineas sin dato se omiten. WhatsApp interpreta *texto* como negrilla.</summary>
    public static string ArmarTexto(clsDatosOrdenTanqueo orden)
    {
        var lineas = new List<string> { $"{ICONO_CAMION} *Orden de tanqueo*" };
        if (!string.IsNullOrWhiteSpace(orden.Placa)) lineas.Add($"Placa: {orden.Placa}");
        if (!string.IsNullOrWhiteSpace(orden.Manifiesto)) lineas.Add($"Manifiesto: {orden.Manifiesto}");
        if (!string.IsNullOrWhiteSpace(orden.Ruta)) lineas.Add($"Ruta: {orden.Ruta}");
        lineas.Add($"Estación: {orden.Estacion}");
        lineas.Add($"Galones autorizados: {orden.GalonesAutorizados.ToString("0.###", CultureInfo.InvariantCulture)}");
        if (!string.IsNullOrWhiteSpace(orden.MapaUrl)) lineas.Add($"Ubicación: {orden.MapaUrl}");
        if (!string.IsNullOrWhiteSpace(orden.BitacoraUrl))
            lineas.Add($"Reporta tu tanqueo (foto del surtidor + odómetro): {orden.BitacoraUrl}");
        return string.Join('\n', lineas);
    }

    /// <summary>Enlace de Google Maps a las coordenadas de la estacion, o null si no tiene.</summary>
    public static string? ArmarMapaUrl(double? lat, double? lng) =>
        lat is { } la && lng is { } lo
            ? string.Create(CultureInfo.InvariantCulture, $"https://www.google.com/maps?q={la},{lo}")
            : null;
}
