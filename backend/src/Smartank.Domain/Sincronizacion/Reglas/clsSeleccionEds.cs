using Smartank.Domain.Geo;

namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Selecciona las EDS de ACPM cercanas a la polilinea de un viaje para el precio de referencia del panel de margen.</summary>
public static class clsSeleccionEds
{
    private const double KM_POR_GRADO = 111.0;

    /// <summary>
    /// Una EDS esta "cerca" si su distancia a la ruta es &lt;= umbral. Se filtra primero por la caja envolvente de la ruta
    /// (ampliada por el umbral) para no medir las ~2.000 estaciones contra cada ruta, y la distancia se mide contra los
    /// SEGMENTOS (clsGeo), no contra el vertice mas cercano como hacia el origen: es mas exacta en rutas simplificadas
    /// con segmentos largos.
    /// </summary>
    public static clsPrecioReferenciaRuta Calcular(
        IReadOnlyList<clsCoordenada> polilinea, IReadOnlyList<clsEdsCandidata> eds, double umbralKm)
    {
        if (polilinea.Count < 2 || eds.Count == 0) return new clsPrecioReferenciaRuta(null, null, 0);

        double minLat = double.PositiveInfinity, maxLat = double.NegativeInfinity;
        double minLng = double.PositiveInfinity, maxLng = double.NegativeInfinity;
        foreach (var p in polilinea)
        {
            if (p.Lat < minLat) minLat = p.Lat;
            if (p.Lat > maxLat) maxLat = p.Lat;
            if (p.Lng < minLng) minLng = p.Lng;
            if (p.Lng > maxLng) maxLng = p.Lng;
        }

        var gradosLat = umbralKm / KM_POR_GRADO;
        var latMedia = (minLat + maxLat) / 2;
        var gradosLng = umbralKm / (KM_POR_GRADO * Math.Max(0.2, Math.Cos(latMedia * Math.PI / 180.0)));

        decimal? min = null, max = null;
        var cerca = 0;
        foreach (var e in eds)
        {
            if (e.Lat < minLat - gradosLat || e.Lat > maxLat + gradosLat) continue;
            if (e.Lng < minLng - gradosLng || e.Lng > maxLng + gradosLng) continue;
            if (clsGeo.DistanciaAPolilineaKm(new clsCoordenada(e.Lat, e.Lng), polilinea) > umbralKm) continue;

            cerca++;
            if (min is null || e.PrecioAcpm < min) min = e.PrecioAcpm;
            if (max is null || e.PrecioAcpm > max) max = e.PrecioAcpm;
        }
        return new clsPrecioReferenciaRuta(min, max, cerca);
    }
}
