using System.Globalization;
using System.Text;
using Smartank.Domain.Geo;

namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Arma la polilinea almacenada de una ruta segun su fuente (Valhalla detallada o Dane recta origen-destino).</summary>
public static class clsRutaPolilinea
{
    /// <summary>
    /// Polilinea Valhalla: geometria COMPLETA y ordenada del legado (WKT de STAsText) simplificada con Douglas-Peucker a la
    /// tolerancia dada. Nunca Geom.Reduce() de SQL Server: reordena el trazado.
    /// </summary>
    public static List<clsCoordenada> DesdeValhalla(string? wkt, double tolMetros) =>
        clsPolilinea.Simplificar(clsPolilinea.ParseLineStringWkt(wkt), tolMetros);

    /// <summary>Polilinea Dane: linea recta entre las coordenadas de los centros poblados de origen y destino (2 puntos).</summary>
    public static List<clsCoordenada> DesdeDane(clsCoordenada origen, clsCoordenada destino) => [origen, destino];

    /// <summary>Longitud en km (2 decimales) de una polilinea; sirve de kilometros de la ruta Dane, que no trae el dato.</summary>
    public static double Kilometros(IReadOnlyList<clsCoordenada> puntos) =>
        Math.Round(clsGeo.LongitudPolilineaKm(puntos), 2, MidpointRounding.AwayFromZero);

    /// <summary>JSON de la columna Polilinea: <c>[[lat,lng],...]</c> con punto decimal (la base exige ISJSON = 1).</summary>
    public static string AJson(IReadOnlyList<clsCoordenada> puntos)
    {
        var sb = new StringBuilder(puntos.Count * 24 + 2);
        sb.Append('[');
        for (var i = 0; i < puntos.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('[')
              .Append(puntos[i].Lat.ToString("R", CultureInfo.InvariantCulture))
              .Append(',')
              .Append(puntos[i].Lng.ToString("R", CultureInfo.InvariantCulture))
              .Append(']');
        }
        return sb.Append(']').ToString();
    }
}
