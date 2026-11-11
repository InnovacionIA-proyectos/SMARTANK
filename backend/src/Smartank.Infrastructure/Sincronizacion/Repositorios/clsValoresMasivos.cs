namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>
/// Conversiones de valores .NET a lo que espera la columna de la base. Los decimales se redondean a la escala de la columna
/// para que la carga masiva no falle por escala ni redondee de forma distinta a la del origen; null/NaN/infinito van como NULL.
/// </summary>
public static class clsValoresMasivos
{
    public static object? Decimal(decimal? valor, int escala) =>
        valor is { } v ? Math.Round(v, escala, MidpointRounding.AwayFromZero) : null;

    public static object? Decimal(double? valor, int escala) =>
        valor is { } v && double.IsFinite(v) && Math.Abs(v) < 1e15 ? Math.Round((decimal)v, escala, MidpointRounding.AwayFromZero) : null;

    /// <summary>Un texto vacio se guarda NULL.</summary>
    public static object? Texto(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor;
}
