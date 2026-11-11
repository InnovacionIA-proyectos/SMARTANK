using System.Text.Json;

namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>El detalle de tramos del legado llega como texto: la columna DetalleKm exige JSON valido (ISJSON = 1) o NULL.</summary>
public static class clsJsonSeguro
{
    /// <summary>Devuelve el JSON compactado si el texto es valido; null si esta vacio o no es JSON (como el origen, que descartaba lo invalido).</summary>
    public static string? NormalizarONull(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        try
        {
            using var doc = JsonDocument.Parse(texto);
            // "null" literal tambien es JSON valido pero no aporta dato: se guarda NULL en la base.
            return doc.RootElement.ValueKind == JsonValueKind.Null ? null : JsonSerializer.Serialize(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
