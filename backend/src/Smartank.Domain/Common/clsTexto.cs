using System.Text.RegularExpressions;

namespace Smartank.Domain.Common;

/// <summary>Normalizaciones de texto del dominio (datos que llegan sucios del SQL Server legado o de la app movil).</summary>
public static partial class clsTexto
{
    /// <summary>Recorta espacios y convierte vacio en null.</summary>
    public static string? NullifyBlank(string? valor)
    {
        if (valor is null) return null;
        var t = valor.Trim();
        return t.Length == 0 ? null : t;
    }

    /// <summary>Ademas de recortar, trata "N/A" (frecuente en Distracom) como null.</summary>
    public static string? NullifyNA(string? valor)
    {
        var t = NullifyBlank(valor);
        return t is not null && !string.Equals(t, "N/A", StringComparison.OrdinalIgnoreCase) ? t : null;
    }

    /// <summary>Quita los ceros a la izquierda de un numero de FO ("000301453730" -> "301453730"). Si eran todos ceros devuelve el original recortado.</summary>
    public static string? StripCeros(string? valor)
    {
        var t = NullifyBlank(valor);
        if (t is null) return null;
        var sin = t.TrimStart('0');
        return sin.Length == 0 ? t : sin;
    }

    /// <summary>
    /// Normaliza un codigo DANE / de ciudad a 12 digitos con ceros a la izquierda. NO se recortan ceros: hacerlo colapsaba
    /// departamentos que empiezan por 0 (05 Antioquia, 08 Atlantico) contra otros y daba coordenadas equivocadas.
    /// </summary>
    public static string? NormalizarCodigoDane(string? valor)
    {
        var t = NullifyBlank(valor);
        return t?.PadLeft(12, '0');
    }

    /// <summary>
    /// Normaliza un celular colombiano a formato E.164 (+57XXXXXXXXXX). Acepta 10 digitos que empiezan por 3 ("3046758799")
    /// o ya con indicativo (57 + 10 digitos). Devuelve null si no es un celular colombiano valido (descarta, por ejemplo,
    /// una cedula escrita en el campo de celular).
    /// </summary>
    public static string? NormalizarCelularCo(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var digitos = SoloDigitos().Replace(valor, "");
        if (Regex.IsMatch(digitos, @"^3\d{9}$")) return $"+57{digitos}";
        if (Regex.IsMatch(digitos, @"^573\d{9}$")) return $"+{digitos}";
        return null;
    }

    [GeneratedRegex(@"\D")]
    private static partial Regex SoloDigitos();
}
