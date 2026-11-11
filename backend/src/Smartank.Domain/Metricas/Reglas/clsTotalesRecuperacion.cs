namespace Smartank.Domain.Metricas.Reglas;

/// <summary>
/// Totales de recuperacion de margen. Gasto real = lo efectivamente pagado; ideal = lo que habria costado en la EDS
/// mas barata cercana a la ruta; maximo = en la mas cara. Los tanqueos sin referencia de ruta valen su propio precio
/// pagado en ideal y maximo, de modo que no aportan ahorro ni oportunidad ficticios.
/// </summary>
public sealed record clsTotalesRecuperacion(
    decimal Galones,
    int Tanqueos,
    decimal GastoReal,
    decimal GastoIdeal,
    decimal GastoMaximo,
    decimal? PrecioMinimoRuta)
{
    /// <summary>KPI principal: cuanto se dejo de gastar frente a tanquear siempre en la EDS mas cara.</summary>
    public decimal AhorroVsMaximo => GastoMaximo - GastoReal;

    /// <summary>Margen por capturar: cuanto mas se pago que tanqueando siempre en la EDS mas barata.</summary>
    public decimal OportunidadVsIdeal => GastoReal - GastoIdeal;
}
