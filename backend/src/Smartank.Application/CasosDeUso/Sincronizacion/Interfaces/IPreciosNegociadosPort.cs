namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>
/// ACPM negociado (Prodes) por EDS: esas estaciones conservan el precio negociado y el de los mapas no lo sobrescribe.
/// Dato de apoyo versionado en el codigo; sin datos reales de otra fuente no se inventan precios.
/// </summary>
public interface IPreciosNegociadosPort
{
    /// <summary>Precio ACPM por nombre de EDS normalizado (<c>clsReglasPrecios.NormalizarNombre</c>).</summary>
    IReadOnlyDictionary<string, decimal> ObtenerAcpmPorNombre();
}
