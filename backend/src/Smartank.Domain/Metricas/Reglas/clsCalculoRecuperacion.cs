namespace Smartank.Domain.Metricas.Reglas;

/// <summary>
/// Recuperacion de margen del tablero de Finanzas, como reglas puras sobre tanqueos ya filtrados y ligados a su ruta
/// (la consulta vive en Infrastructure). Port fiel de las agregaciones de la consulta original:
/// <list type="bullet">
/// <item>Gasto real = suma de galones x precio pagado.</item>
/// <item>Gasto ideal = suma de galones x ACPM minimo de la ruta (si no hay referencia, el precio pagado).</item>
/// <item>Gasto maximo = suma de galones x ACPM maximo de la ruta (si no hay referencia, el precio pagado).</item>
/// </list>
/// Las sumas ignoran los valores nulos (un tanqueo sin galones o sin precio no suma, pero si cuenta como tanqueo).
/// </summary>
public static class clsCalculoRecuperacion
{
    /// <summary>Nombre con el que se agrupan los tanqueos que no traen proveedor.</summary>
    public const string SIN_PROVEEDOR = "(sin proveedor)";

    /// <summary>Texto de la estacion cuando el tanqueo del equipo no la trae.</summary>
    public const string EDS_DESCONOCIDA = "—";

    public static clsResultadoRecuperacion Calcular(IReadOnlyCollection<clsTanqueoMetrica> tanqueos) =>
        new(
            CalcularTotales(tanqueos),
            CalcularUtilizacion(tanqueos),
            CalcularEquipos(tanqueos),
            CalcularGalonesPorEds(tanqueos),
            CalcularRutas(tanqueos));

    public static clsTotalesRecuperacion CalcularTotales(IReadOnlyCollection<clsTanqueoMetrica> tanqueos)
    {
        var galones = tanqueos.Sum(t => t.Galones ?? 0m);
        var real = tanqueos.Sum(t => Importe(t.Galones, t.PrecioPagado) ?? 0m);
        var ideal = tanqueos.Sum(t => Importe(t.Galones, t.AcpmMin ?? t.PrecioPagado) ?? 0m);
        var maximo = tanqueos.Sum(t => Importe(t.Galones, t.AcpmMax ?? t.PrecioPagado) ?? 0m);
        var minimo = tanqueos.Where(t => t.AcpmMin is not null).Select(t => t.AcpmMin).Min();
        return new clsTotalesRecuperacion(galones, tanqueos.Count, real, ideal, maximo, minimo);
    }

    /// <summary>Tanqueos y galones por proveedor, de mayor a menor numero de tanqueos.</summary>
    public static IReadOnlyList<clsUtilizacionEds> CalcularUtilizacion(IReadOnlyCollection<clsTanqueoMetrica> tanqueos) =>
        tanqueos
            .GroupBy(t => t.Proveedor ?? SIN_PROVEEDOR)
            .Select(g => new clsUtilizacionEds(g.Key, g.Count(), g.Sum(t => t.Galones ?? 0m)))
            .OrderByDescending(u => u.Tanqueos)
            .ThenBy(u => u.Proveedor, StringComparer.Ordinal)
            .ToList();

    /// <summary>
    /// El tanqueo de menor valor por galon de cada equipo en cada mes (ante empate, el mas antiguo). El valor por galon
    /// es el precio pagado o, si no viene, valor total / galones; sin ninguno de los dos el tanqueo no compite.
    /// </summary>
    public static IReadOnlyList<clsEquipoFila> CalcularEquipos(IReadOnlyCollection<clsTanqueoMetrica> tanqueos) =>
        tanqueos
            .Select(t => (Tanqueo: t, Valor: ValorPorGalon(t)))
            .Where(x => x.Valor is not null)
            .GroupBy(x => (x.Tanqueo.Placa, x.Tanqueo.Fecha.Month))
            .Select(g => g.OrderBy(x => x.Valor!.Value).ThenBy(x => x.Tanqueo.Fecha).First())
            .Select(x => new clsEquipoFila(
                x.Tanqueo.Placa,
                x.Tanqueo.Fecha.Month,
                x.Tanqueo.Fecha,
                x.Valor!.Value,
                x.Tanqueo.Galones ?? 0m,
                x.Tanqueo.ValorTotal ?? 0m,
                x.Tanqueo.Estacion ?? EDS_DESCONOCIDA,
                x.Tanqueo.Ciudad))
            .OrderBy(e => e.Equipo, StringComparer.Ordinal)
            .ThenBy(e => e.Mes)
            .ToList();

    /// <summary>Galones y tanqueos por estacion del catalogo (solo los tanqueos que se pudieron casar), de mayor a menor volumen.</summary>
    public static IReadOnlyList<clsGalonesPorEds> CalcularGalonesPorEds(IReadOnlyCollection<clsTanqueoMetrica> tanqueos) =>
        tanqueos
            .Where(t => t.EstacionCatalogo is not null)
            .GroupBy(t => t.EstacionCatalogo!.Id)
            .Select(g =>
            {
                var e = g.First().EstacionCatalogo!;
                return new clsGalonesPorEds(
                    e.Id, e.Nombre, e.Lat, e.Lng, null, e.Proveedor, e.PrecioAcpm,
                    g.Sum(t => t.Galones ?? 0m), g.Count());
            })
            .OrderByDescending(e => e.Galones)
            .ThenBy(e => e.Nombre, StringComparer.Ordinal)
            .ToList();

    /// <summary>Segmentos origen-destino distintos de las rutas de los tanqueos que tienen extremos conocidos.</summary>
    public static IReadOnlyList<clsRutaLinea> CalcularRutas(IReadOnlyCollection<clsTanqueoMetrica> tanqueos) =>
        tanqueos
            .Where(t => t.ExtremosRuta is not null)
            .Select(t => new clsRutaLinea(
                t.Ruta,
                t.ExtremosRuta!.OrigenLat, t.ExtremosRuta.OrigenLng,
                t.ExtremosRuta.DestinoLat, t.ExtremosRuta.DestinoLng))
            .Distinct()
            .OrderBy(r => r.Ruta, StringComparer.Ordinal)
            .ThenBy(r => r.OrigenLat)
            .ThenBy(r => r.OrigenLng)
            .ToList();

    /// <summary>Valor por galon de un tanqueo: precio pagado o, en su defecto, valor total / galones (null si galones es 0 o nulo).</summary>
    public static decimal? ValorPorGalon(clsTanqueoMetrica t)
    {
        if (t.PrecioPagado is not null) return t.PrecioPagado;
        if (t.ValorTotal is not null && t.Galones is { } g and not 0m) return t.ValorTotal / g;
        return null;
    }

    private static decimal? Importe(decimal? galones, decimal? precio) =>
        galones is null || precio is null ? null : galones * precio;
}
