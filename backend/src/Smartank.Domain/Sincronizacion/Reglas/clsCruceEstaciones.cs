using Smartank.Domain.Geo;

namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>
/// Indice de las estaciones existentes de un proveedor para encontrar la que corresponde a un registro de la fuente.
/// Distracom cruza por codigo externo (EstacionID); Terpel no expone codigo, asi que cruza por coordenadas cercanas
/// (&lt;= 0.2 km, la mas cercana) y, en ultimo caso, por nombre normalizado.
/// </summary>
public sealed class clsCruceEstaciones
{
    private readonly IReadOnlyList<clsEstacionReferencia> _existentes;
    private readonly Dictionary<string, clsEstacionReferencia> _porCodigo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, clsEstacionReferencia> _porNombre = new(StringComparer.Ordinal);

    public clsCruceEstaciones(IReadOnlyList<clsEstacionReferencia> existentes)
    {
        _existentes = existentes;
        foreach (var e in existentes)
        {
            if (!string.IsNullOrEmpty(e.CodigoExterno)) _porCodigo[e.CodigoExterno] = e;
            _porNombre[clsReglasPrecios.NormalizarNombre(e.Nombre)] = e;
        }
    }

    /// <summary>Busca la estacion existente que corresponde a la de la fuente, o null si es nueva.</summary>
    public clsEstacionReferencia? Encontrar(string? codigoExterno, string? nombre, double? lat, double? lng)
    {
        if (!string.IsNullOrEmpty(codigoExterno) && _porCodigo.TryGetValue(codigoExterno, out var porCodigo)) return porCodigo;

        if (lat is { } la && lng is { } lo)
        {
            clsEstacionReferencia? mejor = null;
            var mejorKm = clsReglasPrecios.UMBRAL_MISMA_ESTACION_KM;
            foreach (var e in _existentes)
            {
                if (e.Lat is not { } elat || e.Lng is not { } elng) continue;
                var d = clsGeo.DistanciaKm(la, lo, elat, elng);
                if (d <= mejorKm)
                {
                    mejorKm = d;
                    mejor = e;
                }
            }
            if (mejor is not null) return mejor;
        }

        return _porNombre.TryGetValue(clsReglasPrecios.NormalizarNombre(nombre), out var porNombre) ? porNombre : null;
    }
}
