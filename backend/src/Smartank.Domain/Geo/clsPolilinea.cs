using System.Globalization;
using System.Text.RegularExpressions;

namespace Smartank.Domain.Geo;

/// <summary>
/// Lectura y simplificacion de polilineas de ruta. IMPORTANTE: no usar Geom.Reduce() de SQL Server sobre estas rutas:
/// devuelve un MULTILINESTRING con los tramos DESORDENADOS y al concatenarlos la ruta queda revuelta. Se consulta la
/// geometria completa (Geom.STAsText(), una LineString ordenada) y la simplificacion se hace aqui.
/// </summary>
public static partial class clsPolilinea
{
    private const double METROS_POR_GRADO = 111_320.0;
    private const double TOLERANCIA_POR_DEFECTO_METROS = 30.0;

    [GeneratedRegex(@"-?\d+(?:\.\d+)?\s+-?\d+(?:\.\d+)?")]
    private static partial Regex ParLonLat();

    /// <summary>Parsea un WKT "LINESTRING (lon lat, ...)" a puntos (lat, lng) en el orden del texto.</summary>
    public static List<clsCoordenada> ParseLineStringWkt(string? wkt)
    {
        var puntos = new List<clsCoordenada>();
        if (string.IsNullOrEmpty(wkt)) return puntos;
        foreach (Match par in ParLonLat().Matches(wkt))
        {
            var partes = par.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (partes.Length != 2) continue;
            if (double.TryParse(partes[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)
                && double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat))
                puntos.Add(new clsCoordenada(lat, lon));
        }
        return puntos;
    }

    /// <summary>
    /// Douglas-Peucker conservando ORDEN y EXTREMOS. <paramref name="tolMetros"/> es la distancia perpendicular maxima
    /// tolerada, convertida a grados (~111.32 km por grado).
    /// </summary>
    public static List<clsCoordenada> Simplificar(IReadOnlyList<clsCoordenada> puntos, double tolMetros = TOLERANCIA_POR_DEFECTO_METROS)
    {
        if (puntos.Count <= 2) return puntos.ToList();
        var eps = (double.IsFinite(tolMetros) && tolMetros > 0 ? tolMetros : TOLERANCIA_POR_DEFECTO_METROS) / METROS_POR_GRADO;

        var conservar = new bool[puntos.Count];
        conservar[0] = true;
        conservar[^1] = true;
        var pila = new Stack<(int Ini, int Fin)>();
        pila.Push((0, puntos.Count - 1));
        while (pila.Count > 0)
        {
            var (ini, fin) = pila.Pop();
            var dmax = 0.0;
            var idx = -1;
            for (var i = ini + 1; i < fin; i++)
            {
                var d = DistanciaPerpendicular(puntos[i], puntos[ini], puntos[fin]);
                if (d > dmax)
                {
                    dmax = d;
                    idx = i;
                }
            }
            if (dmax > eps && idx != -1)
            {
                conservar[idx] = true;
                pila.Push((ini, idx));
                pila.Push((idx, fin));
            }
        }

        var salida = new List<clsCoordenada>();
        for (var i = 0; i < puntos.Count; i++)
            if (conservar[i]) salida.Add(puntos[i]);
        return salida;
    }

    /// <summary>Distancia perpendicular de p al segmento (a,b) en espacio de grados.</summary>
    private static double DistanciaPerpendicular(clsCoordenada p, clsCoordenada a, clsCoordenada b)
    {
        var dx = b.Lng - a.Lng;
        var dy = b.Lat - a.Lat;
        if (dx == 0 && dy == 0) return Math.Sqrt(Math.Pow(p.Lng - a.Lng, 2) + Math.Pow(p.Lat - a.Lat, 2));
        var t = ((p.Lng - a.Lng) * dx + (p.Lat - a.Lat) * dy) / (dx * dx + dy * dy);
        var cx = a.Lng + t * dx;
        var cy = a.Lat + t * dy;
        return Math.Sqrt(Math.Pow(p.Lng - cx, 2) + Math.Pow(p.Lat - cy, 2));
    }
}
