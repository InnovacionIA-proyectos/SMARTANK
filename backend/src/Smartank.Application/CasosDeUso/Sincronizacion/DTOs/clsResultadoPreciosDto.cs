namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>
/// Resultado de la sincronizacion automatica de precios con la forma del contrato original
/// (<c>{ modo: "automatico", fuente: { distracom: {...}, terpel: {...} } }</c>) mas la lista de errores (aditiva).
/// </summary>
public sealed record clsResultadoPreciosDto(
    string Modo,
    IReadOnlyDictionary<string, clsResultadoFuentePreciosDto> Fuente,
    IReadOnlyList<string> Errores)
{
    /// <summary>Vista comun (contadores agregados de ambas redes) para el Worker.</summary>
    public clsResultadoSincronizacion ComoResumen()
    {
        var errores = Errores.Select(e => new clsErrorSincronizacion("precios", e)).ToList();
        var extras = Fuente.ToDictionary(f => $"{f.Key}Total", f => f.Value.Total);
        return clsResultadoSincronizacion.Crear(
            Fuente.Values.Sum(f => f.Total),
            Fuente.Values.Sum(f => f.Creadas),
            Fuente.Values.Sum(f => f.Actualizadas),
            0,
            errores,
            extras);
    }
}
