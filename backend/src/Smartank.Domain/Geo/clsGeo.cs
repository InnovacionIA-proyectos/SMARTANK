namespace Smartank.Domain.Geo;

/// <summary>Par (latitud, longitud) en grados. En el contrato JSON una polilinea viaja como [[lat, lng], ...].</summary>
public readonly record struct clsCoordenada(double Lat, double Lng);

/// <summary>Ubicacion de un punto SOBRE una polilinea: km recorridos a lo largo de la ruta y distancia perpendicular a ella.</summary>
public readonly record struct clsPosicionSobrePolilinea(double PosKm, double DistKm);

/// <summary>
/// Geometria sobre la esfera/plano local usada para ubicar vehiculos y estaciones sobre la ruta de un viaje.
/// Port fiel de la logica original (haversine + proyeccion equirectangular local sobre cada segmento).
/// </summary>
public static class clsGeo
{
    private const double RADIO_TIERRA_KM = 6371.0;

    private static double Rad(double grados) => grados * Math.PI / 180.0;

    /// <summary>Distancia en km entre dos puntos (haversine).</summary>
    public static double DistanciaKm(double aLat, double aLng, double bLat, double bLng)
    {
        var dLat = Rad(bLat - aLat);
        var dLng = Rad(bLng - aLng);
        var h = Math.Pow(Math.Sin(dLat / 2), 2)
                + Math.Cos(Rad(aLat)) * Math.Cos(Rad(bLat)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * RADIO_TIERRA_KM * Math.Asin(Math.Sqrt(h));
    }

    /// <summary>Proyecta a un plano local en km (equirectangular alrededor de latRef en radianes).</summary>
    private static (double X, double Y) Plano(clsCoordenada p, double latRefRad) =>
        (Rad(p.Lng) * Math.Cos(latRefRad) * RADIO_TIERRA_KM, Rad(p.Lat) * RADIO_TIERRA_KM);

    /// <summary>Proyeccion del punto sobre el segmento a-b: parametro t acotado a [0,1], proyeccion y distancia (km).</summary>
    private static (double T, double Cx, double Cy, double Dist, double LongSegmento, double LatRefRad) ProyectarEnSegmento(
        clsCoordenada punto, clsCoordenada a, clsCoordenada b)
    {
        var latRef = Rad((a.Lat + b.Lat) / 2);
        var (px, py) = Plano(punto, latRef);
        var (ax, ay) = Plano(a, latRef);
        var (bx, by) = Plano(b, latRef);
        var dx = bx - ax;
        var dy = by - ay;
        var len2 = dx * dx + dy * dy;
        var t = len2 == 0 ? 0 : ((px - ax) * dx + (py - ay) * dy) / len2;
        t = Math.Max(0, Math.Min(1, t));
        var cx = ax + t * dx;
        var cy = ay + t * dy;
        var dist = Math.Sqrt(Math.Pow(px - cx, 2) + Math.Pow(py - cy, 2));
        return (t, cx, cy, dist, Math.Sqrt(len2), latRef);
    }

    /// <summary>Punto MAS CERCANO sobre la polilinea a un punto dado (sirve para "pegar" el marcador del vehiculo a la ruta).</summary>
    public static clsCoordenada ProyectarEnPolilinea(clsCoordenada punto, IReadOnlyList<clsCoordenada> polilinea)
    {
        if (polilinea.Count == 0) return punto;
        if (polilinea.Count == 1) return polilinea[0];
        var mejor = polilinea[0];
        var mejorDist = double.PositiveInfinity;
        for (var i = 0; i < polilinea.Count - 1; i++)
        {
            var (_, cx, cy, dist, _, latRef) = ProyectarEnSegmento(punto, polilinea[i], polilinea[i + 1]);
            if (dist < mejorDist)
            {
                mejorDist = dist;
                mejor = new clsCoordenada(
                    cy / RADIO_TIERRA_KM * (180.0 / Math.PI),
                    cx / (RADIO_TIERRA_KM * Math.Cos(latRef)) * (180.0 / Math.PI));
            }
        }
        return mejor;
    }

    /// <summary>Distancia minima (km) de un punto a una polilinea (minimo sobre todos sus segmentos).</summary>
    public static double DistanciaAPolilineaKm(clsCoordenada punto, IReadOnlyList<clsCoordenada> polilinea)
    {
        if (polilinea.Count == 0) return double.PositiveInfinity;
        if (polilinea.Count == 1) return DistanciaKm(punto.Lat, punto.Lng, polilinea[0].Lat, polilinea[0].Lng);
        var min = double.PositiveInfinity;
        for (var i = 0; i < polilinea.Count - 1; i++)
        {
            var (_, _, _, dist, _, _) = ProyectarEnSegmento(punto, polilinea[i], polilinea[i + 1]);
            if (dist < min) min = dist;
        }
        return min;
    }

    /// <summary>
    /// Ubica un punto SOBRE una polilinea: km recorridos a lo largo de la ruta hasta la proyeccion del punto y distancia
    /// perpendicular (km). Proyecta sobre segmentos (no sobre vertices): es exacta incluso en polilineas de pocos puntos.
    /// </summary>
    public static clsPosicionSobrePolilinea PosicionSobrePolilineaKm(clsCoordenada punto, IReadOnlyList<clsCoordenada> polilinea)
    {
        if (polilinea.Count == 0) return new clsPosicionSobrePolilinea(0, double.PositiveInfinity);
        if (polilinea.Count == 1)
            return new clsPosicionSobrePolilinea(0, DistanciaKm(punto.Lat, punto.Lng, polilinea[0].Lat, polilinea[0].Lng));

        var acumulado = 0.0;
        var mejorPos = 0.0;
        var mejorDist = double.PositiveInfinity;
        for (var i = 0; i < polilinea.Count - 1; i++)
        {
            var (t, _, _, dist, longSeg, _) = ProyectarEnSegmento(punto, polilinea[i], polilinea[i + 1]);
            if (dist < mejorDist)
            {
                mejorDist = dist;
                mejorPos = acumulado + t * longSeg;
            }
            acumulado += longSeg;
        }
        return new clsPosicionSobrePolilinea(mejorPos, mejorDist);
    }

    /// <summary>Fraccion [0,1] de avance de un punto sobre la recta origen-destino (0 = origen, 1 = destino).</summary>
    public static double ProgresoRuta(clsCoordenada origen, clsCoordenada destino, clsCoordenada punto)
    {
        var latRef = Rad((origen.Lat + destino.Lat) / 2);
        var (ox, oy) = Plano(origen, latRef);
        var (dxp, dyp) = Plano(destino, latRef);
        var (px, py) = Plano(punto, latRef);
        var dx = dxp - ox;
        var dy = dyp - oy;
        var len2 = dx * dx + dy * dy;
        if (len2 == 0) return 0;
        var t = ((px - ox) * dx + (py - oy) * dy) / len2;
        return Math.Max(0, Math.Min(1, t));
    }

    /// <summary>Longitud total (km) de una polilinea.</summary>
    public static double LongitudPolilineaKm(IReadOnlyList<clsCoordenada> polilinea)
    {
        var total = 0.0;
        for (var i = 1; i < polilinea.Count; i++)
            total += DistanciaKm(polilinea[i - 1].Lat, polilinea[i - 1].Lng, polilinea[i].Lat, polilinea[i].Lng);
        return total;
    }
}
