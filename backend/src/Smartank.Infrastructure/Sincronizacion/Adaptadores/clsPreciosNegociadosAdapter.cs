using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Infrastructure.Sincronizacion.Adaptadores;

/// <summary>ACPM negociado (Prodes) compilado en <see cref="clsPreciosNegociadosDatos"/>; se indexa una vez por nombre normalizado.</summary>
public sealed class clsPreciosNegociadosAdapter : IPreciosNegociadosPort
{
    private static readonly IReadOnlyDictionary<string, decimal> PRECIOS = Construir();

    public IReadOnlyDictionary<string, decimal> ObtenerAcpmPorNombre() => PRECIOS;

    private static Dictionary<string, decimal> Construir()
    {
        var mapa = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var (nombre, precio) in clsPreciosNegociadosDatos.FILAS)
            mapa[clsReglasPrecios.NormalizarNombre(nombre)] = precio;
        return mapa;
    }
}
