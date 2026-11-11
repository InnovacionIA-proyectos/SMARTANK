using System.Globalization;
using Smartank.Domain.Common;

namespace Smartank.Infrastructure.Legacy;

/// <summary>
/// Fila del SQL Server legado con lectura tolerante por nombre de columna (MAYUSCULAS). Se lee como diccionario y no como
/// clase tipada porque los tipos de las columnas del legado (int/bigint, decimal/float, bit/int) no son un contrato que
/// controlemos: cada accesor convierte lo que llegue.
/// </summary>
public readonly struct clsFilaLegada(IDictionary<string, object?> campos)
{
    private object? Valor(string columna) => campos.TryGetValue(columna, out var v) && v is not DBNull ? v : null;

    /// <summary>Texto recortado ("" y espacios -> null).</summary>
    public string? Texto(string columna) => clsTexto.NullifyBlank(Convert.ToString(Valor(columna), CultureInfo.InvariantCulture));

    /// <summary>Texto sin el valor "N/A" (frecuente en Distracom).</summary>
    public string? TextoSinNA(string columna) => clsTexto.NullifyNA(Convert.ToString(Valor(columna), CultureInfo.InvariantCulture));

    /// <summary>Numero entero mostrado como texto sin decimales ("1234.0000" -> "1234"); null si no hay valor.</summary>
    public string? NumeroComoTexto(string columna) => Valor(columna) switch
    {
        null => null,
        string s => clsTexto.NullifyBlank(s),
        var v => Convert.ToDecimal(v, CultureInfo.InvariantCulture).ToString("0.##########", CultureInfo.InvariantCulture),
    };

    public long Entero(string columna) => Valor(columna) is { } v ? Convert.ToInt64(v, CultureInfo.InvariantCulture) : 0;

    public decimal? Decimal(string columna) => Valor(columna) is { } v ? Convert.ToDecimal(v, CultureInfo.InvariantCulture) : null;

    public double? Doble(string columna) => Valor(columna) is { } v ? Convert.ToDouble(v, CultureInfo.InvariantCulture) : null;

    /// <summary>bit (o numerico 0/1); NULL cuenta como false, igual que la comparacion estricta del origen.</summary>
    public bool Booleano(string columna) => Valor(columna) switch
    {
        bool b => b,
        null => false,
        var v => Convert.ToInt64(v, CultureInfo.InvariantCulture) != 0,
    };

    /// <summary>datetime tal cual lo entrega SqlClient (Kind Unspecified = hora de Colombia en el legado).</summary>
    public DateTime? Fecha(string columna) => Valor(columna) switch
    {
        DateTime d => d,
        DateTimeOffset o => o.UtcDateTime,
        _ => null,
    };
}
