using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Reglas puras de la sincronizacion de precios de EDS (Terpel y Distracom).</summary>
public static partial class clsReglasPrecios
{
    // Banda realista de COP/galon: descarta 0, los placeholders (1) y errores de origen (1170, 112450).
    public const double PRECIO_MIN = 4000;
    public const double PRECIO_MAX = 40000;

    /// <summary>Radio (km) para considerar que una EDS de la fuente es la misma que una del catalogo.</summary>
    public const double UMBRAL_MISMA_ESTACION_KM = 0.2;

    /// <summary>Devuelve el precio si cae dentro de la banda realista; si no, null.</summary>
    public static decimal? PrecioValido(double? valor) =>
        valor is { } v && double.IsFinite(v) && v >= PRECIO_MIN && v <= PRECIO_MAX ? (decimal)v : null;

    /// <summary>Coordenada dentro de Colombia; fuera de rango (datos basura de la fuente) se descarta.</summary>
    public static bool CoordenadaValida(double lat, double lng) =>
        double.IsFinite(lat) && double.IsFinite(lng) && lat >= -5 && lat <= 16 && lng >= -82 && lng <= -66;

    /// <summary>Nombre en mayusculas, sin tildes y con espacios colapsados: clave de cruce entre catalogo, fuente y precios negociados.</summary>
    public static string NormalizarNombre(string? nombre)
    {
        if (string.IsNullOrWhiteSpace(nombre)) return string.Empty;
        var descompuesto = nombre.ToUpperInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(descompuesto.Length);
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) sb.Append(c);
        return Espacios().Replace(sb.ToString(), " ").Trim();
    }

    /// <summary>
    /// ACPM a registrar: el precio negociado (Prodes) de la EDS prevalece sobre el de los mapas; si la EDS no esta en la
    /// lista negociada se usa el de la fuente.
    /// </summary>
    public static decimal? ResolverAcpm(IReadOnlyDictionary<string, decimal> negociados, string? nombre, decimal? acpmFuente) =>
        negociados.TryGetValue(NormalizarNombre(nombre), out var negociado) ? negociado : acpmFuente;

    /// <summary>Texto de producto de una EDS nueva: "ACPM - Corriente" segun lo que venda; null si no tiene ningun precio.</summary>
    public static string? DescribirProducto(decimal? acpm, decimal? corriente)
    {
        var partes = new List<string>(2);
        if (acpm is not null) partes.Add("ACPM");
        if (corriente is not null) partes.Add("Corriente");
        return partes.Count == 0 ? null : string.Join(" - ", partes);
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Espacios();
}
