namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>
/// Resultado comun de un caso de uso de sincronizacion. <see cref="Total"/> son los registros que llegaron de la fuente;
/// los demas contadores los reparten. Un registro con error no aborta la sincronizacion: se cuenta y se detalla en
/// <see cref="Errores"/>, acotado a <see cref="MAX_ERRORES_DETALLADOS"/> (el conteo real esta en <see cref="ErroresTotal"/>).
/// </summary>
public sealed record clsResultadoSincronizacion
{
    public const int MAX_ERRORES_DETALLADOS = 10;

    public int Total { get; init; }

    public int Insertados { get; init; }

    public int Actualizados { get; init; }

    public int Omitidos { get; init; }

    public int ErroresTotal { get; init; }

    public IReadOnlyList<clsErrorSincronizacion> Errores { get; init; } = [];

    /// <summary>Contadores propios de cada sincronizacion (sin vehiculo, con ubicacion, por fuente...).</summary>
    public IReadOnlyDictionary<string, int> Extras { get; init; } = new Dictionary<string, int>();

    public bool TieneErrores => ErroresTotal > 0;

    /// <summary>Arma el resultado acotando el detalle de errores.</summary>
    public static clsResultadoSincronizacion Crear(
        int total, int insertados, int actualizados, int omitidos,
        IReadOnlyCollection<clsErrorSincronizacion> errores, IReadOnlyDictionary<string, int>? extras = null) =>
        new()
        {
            Total = total,
            Insertados = insertados,
            Actualizados = actualizados,
            Omitidos = omitidos,
            ErroresTotal = errores.Count,
            Errores = errores.Take(MAX_ERRORES_DETALLADOS).ToList(),
            Extras = extras ?? new Dictionary<string, int>(),
        };

    /// <summary>Resumen en una linea para el registro del Worker.</summary>
    public string Resumen()
    {
        var texto = $"total={Total} insertados={Insertados} actualizados={Actualizados} omitidos={Omitidos} errores={ErroresTotal}";
        return Extras.Count == 0 ? texto : texto + " " + string.Join(' ', Extras.Select(e => $"{e.Key}={e.Value}"));
    }
}
