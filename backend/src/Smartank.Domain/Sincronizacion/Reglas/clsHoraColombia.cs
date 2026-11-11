namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>
/// Conversion de la hora de pared del SQL Server legado a instantes UTC. DECISION: las columnas <c>datetime</c> del legado
/// guardan hora de Colombia (UTC-5 fijo, el pais no tiene horario de verano) y SqlClient las entrega con
/// <see cref="DateTimeKind.Unspecified"/>. El origen (Node) las leia como si fueran UTC, lo que adelantaba cada fecha 5 horas
/// respecto del instante real; aqui se interpretan como lo que son para que comparar contra "ahora" sea correcto.
/// </summary>
public static class clsHoraColombia
{
    /// <summary>Desfase fijo de Colombia respecto de UTC.</summary>
    public static readonly TimeSpan OFFSET = TimeSpan.FromHours(-5);

    /// <summary>
    /// Convierte un <c>datetime</c> del legado al instante UTC. Unspecified se interpreta como hora de Colombia; Utc se respeta;
    /// Local usa la zona de la maquina. Un valor tan alto que desborda el rango (centinela 9999-12-31) queda en el maximo.
    /// </summary>
    public static DateTimeOffset AUtc(DateTime valor)
    {
        try
        {
            return valor.Kind switch
            {
                DateTimeKind.Utc => new DateTimeOffset(valor, TimeSpan.Zero),
                DateTimeKind.Local => new DateTimeOffset(valor).ToUniversalTime(),
                _ => new DateTimeOffset(DateTime.SpecifyKind(valor, DateTimeKind.Unspecified), OFFSET).ToUniversalTime(),
            };
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.MaxValue;
        }
    }

    /// <summary>Variante para columnas que admiten NULL.</summary>
    public static DateTimeOffset? AUtc(DateTime? valor) => valor.HasValue ? AUtc(valor.Value) : null;

    /// <summary>Hora de pared de Colombia (Kind Unspecified) de un instante; es lo que entiende el legado al comparar fechas.</summary>
    public static DateTime AHoraColombia(DateTimeOffset instante) =>
        DateTime.SpecifyKind(instante.ToOffset(OFFSET).DateTime, DateTimeKind.Unspecified);

    /// <summary>Fecha calendario de Colombia de un instante (define "una vez por dia" del grupo diario).</summary>
    public static DateOnly FechaColombia(DateTimeOffset instante) => DateOnly.FromDateTime(instante.ToOffset(OFFSET).DateTime);
}
