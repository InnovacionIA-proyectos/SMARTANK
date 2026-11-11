using Smartank.Domain.Geo;

namespace Smartank.Domain.Autorizaciones.Reglas;

/// <summary>EDS con ACPM y coordenadas: la unica clase de estacion que sirve para recomendar (la flota es diesel).</summary>
public readonly record struct clsEstacionCandidata(Guid Id, double Lat, double Lng, decimal PrecioAcpm);

/// <summary>
/// Resultado de elegir el conjunto de EDS donde se buscara la mas barata.
/// <see cref="RutaRestanteKm"/> es null cuando no se pudo medir sobre la ruta (sin polilinea o sin posicion del vehiculo).
/// </summary>
public sealed record clsSeleccionEstaciones(
    IReadOnlyList<clsEstacionCandidata> Consideradas,
    bool SobreRuta,
    double? RutaRestanteKm,
    bool RequiereTanqueo);

/// <summary>Datos del vehiculo que intervienen en el calculo de galones a autorizar.</summary>
public readonly record struct clsVehiculoParaGalones(
    double? RendimientoHistorico,
    double? RendimientoEsperado,
    double ToleranciaMotor,
    decimal? CapacidadTanqueGal,
    decimal? NivelTanqueActual);

/// <summary>
/// Reglas de la recomendacion de tanqueo (port fiel de recomendacion.ts). Todo se mide SOBRE LA RUTA REAL del viaje:
/// si la autonomia cubre lo que falta no hace falta tanquear; si no, se busca la EDS mas barata en la zona de repostaje.
/// Sin ruta o sin posicion del vehiculo se cae a un criterio simple (en linea recta) y se propone tanqueo.
/// </summary>
public static class clsRecomendacionReglas
{
    /// <summary>La EDS debe estar a este maximo (km) de la polilinea: practicamente sobre la via (200 m).</summary>
    public const double UMBRAL_RUTA_KM = 0.2;

    /// <summary>Zona de repostaje: ultimo tramo de la autonomia (desde el 60 % del rango), donde el tanque ya esta bajo.</summary>
    public const double FACTOR_ZONA_AUTONOMIA = 0.6;

    /// <summary>Vigencia de la propuesta, en horas desde que se genera.</summary>
    public const double VIGENCIA_HORAS = 12;

    /// <summary>Galones minimos de una autorizacion: la base exige GalonesMax mayor que cero.</summary>
    private const decimal GALONES_MINIMOS = 1;

    /// <summary>
    /// Elige las EDS candidatas. Con ruta y posicion: solo las cercanas a la ruta y por DELANTE del vehiculo; prefiere las de la
    /// zona de repostaje, luego cualquiera alcanzable, luego la mas cercana por delante; si ninguna esta sobre la ruta, respaldo
    /// en linea recta. <paramref name="candidatas"/> no puede estar vacia (lo valida el caso de uso).
    /// </summary>
    public static clsSeleccionEstaciones Seleccionar(
        IReadOnlyList<clsEstacionCandidata> candidatas,
        IReadOnlyList<clsCoordenada>? polilinea,
        clsCoordenada? posicionVehiculo,
        double autonomiaKm)
    {
        if (polilinea is { Count: > 1 } poli && posicionVehiculo is { } posicion)
        {
            var posVeh = clsGeo.PosicionSobrePolilineaKm(posicion, poli).PosKm;
            var total = clsGeo.LongitudPolilineaKm(poli);
            var rutaRestante = Math.Max(0, total - posVeh);
            // Solo requiere tanqueo si la autonomia NO cubre la ruta restante; la propuesta se genera igual para poder
            // abrir el panel (alli se habilita o no "Autorizar").
            var requiere = rutaRestante > autonomiaKm;

            // Solo las que estan ADELANTE del vehiculo en el sentido de la ruta, nunca detras (hacia el origen).
            var adelante = candidatas
                .Select(e => (Estacion: e, Pos: clsGeo.PosicionSobrePolilineaKm(new clsCoordenada(e.Lat, e.Lng), poli)))
                .Where(x => x.Pos.DistKm <= UMBRAL_RUTA_KM)
                .Select(x => (x.Estacion, Adelante: x.Pos.PosKm - posVeh))
                .Where(x => x.Adelante > 0)
                .ToList();

            var minZona = autonomiaKm * FACTOR_ZONA_AUTONOMIA;
            // 1) zona de repostaje (ultimo tramo del rango).
            var consideradas = adelante
                .Where(x => x.Adelante >= minZona && x.Adelante <= autonomiaKm)
                .Select(x => x.Estacion)
                .ToList();
            // 2) si la zona ideal esta vacia, cualquiera alcanzable hacia adelante.
            if (consideradas.Count == 0)
                consideradas = adelante.Where(x => x.Adelante <= autonomiaKm).Select(x => x.Estacion).ToList();
            // 3) si ninguna cae dentro de la autonomia, la mas cercana hacia adelante (aunque quede algo mas lejos).
            if (consideradas.Count == 0 && adelante.Count > 0)
            {
                var masCercana = adelante.Min(x => x.Adelante);
                consideradas = adelante.Where(x => x.Adelante == masCercana).Select(x => x.Estacion).ToList();
            }

            if (consideradas.Count > 0)
                return new clsSeleccionEstaciones(consideradas, SobreRuta: true, rutaRestante, requiere);

            // Ninguna EDS cercana a la ruta hacia adelante: respaldo por linea recta.
            return new clsSeleccionEstaciones(
                AlcanzablesEnLineaRecta(candidatas, posicionVehiculo, autonomiaKm), SobreRuta: false, rutaRestante, requiere);
        }

        // Sin ruta o sin posicion no se sabe si alcanza: se propone y el control queda en el panel (requiere = true).
        return new clsSeleccionEstaciones(
            AlcanzablesEnLineaRecta(candidatas, posicionVehiculo, autonomiaKm), SobreRuta: false, RutaRestanteKm: null, RequiereTanqueo: true);
    }

    /// <summary>
    /// EDS alcanzables por linea recta dentro de la autonomia desde la posicion del vehiculo. Si no se conoce la posicion o
    /// ninguna queda a rango, devuelve todas (siempre debe haber una opcion que proponer).
    /// </summary>
    public static IReadOnlyList<clsEstacionCandidata> AlcanzablesEnLineaRecta(
        IReadOnlyList<clsEstacionCandidata> candidatas, clsCoordenada? posicionVehiculo, double autonomiaKm)
    {
        if (posicionVehiculo is not { } pos) return candidatas;
        var alcanzables = candidatas
            .Where(e => clsGeo.DistanciaKm(pos.Lat, pos.Lng, e.Lat, e.Lng) <= autonomiaKm)
            .ToList();
        return alcanzables.Count > 0 ? alcanzables : candidatas;
    }

    /// <summary>La mas barata (la primera en caso de empate, orden estable) y la mas cara del conjunto.</summary>
    public static (clsEstacionCandidata Elegida, clsEstacionCandidata MasCara) ElegirMasBarata(
        IReadOnlyList<clsEstacionCandidata> consideradas)
    {
        var ordenadas = consideradas.OrderBy(e => e.PrecioAcpm).ToList();
        return (ordenadas[0], ordenadas[^1]);
    }

    /// <summary>
    /// Galones a autorizar: los necesarios para COMPLETAR la ruta restante (deficit entre lo que falta y la autonomia actual,
    /// convertido con el rendimiento efectivo y topado al espacio libre del tanque). Si el llamador pide galones, mandan esos;
    /// sin ruta restante conocida o sin vehiculo, el valor por defecto. Nunca menos de 1.
    /// </summary>
    public static decimal CalcularGalones(
        decimal? galonesSolicitados,
        double? rutaRestanteKm,
        double autonomiaKm,
        clsVehiculoParaGalones? vehiculo,
        double rendimientoDefaultKmGal,
        double galonesPorDefecto)
    {
        if (galonesSolicitados is { } solicitados) return solicitados;
        var galonesDefault = (decimal)galonesPorDefecto;
        if (rutaRestanteKm is not { } rutaRestante || vehiculo is not { } v) return galonesDefault;

        var rendimiento = v.RendimientoHistorico ?? v.RendimientoEsperado ?? rendimientoDefaultKmGal;
        var rendimientoEfectivo = rendimiento * (1 - v.ToleranciaMotor);
        if (rendimientoEfectivo <= 0) return galonesDefault;

        var deficitKm = Math.Max(0, rutaRestante - autonomiaKm);
        var galones = (decimal)Math.Ceiling(deficitKm / rendimientoEfectivo);

        // Tope: no autorizar mas que el espacio libre del tanque.
        if (v.CapacidadTanqueGal is { } capacidad && v.NivelTanqueActual is { } nivel)
        {
            var libre = Math.Max(0, Math.Round(capacidad - nivel, 0, MidpointRounding.AwayFromZero));
            galones = Math.Min(galones, libre);
        }

        // Si el deficit es 0 (la autonomia cubre la ruta) o el tanque esta lleno, se propone igual con el minimo.
        return Math.Max(GALONES_MINIMOS, galones);
    }

    /// <summary>Ahorro estimado = (precio de la mas cara - precio de la elegida) x galones, redondeado a pesos enteros.</summary>
    public static decimal CalcularAhorro(decimal precioMasCara, decimal precioElegida, decimal galones) =>
        Math.Round((precioMasCara - precioElegida) * galones, 0, MidpointRounding.AwayFromZero);

    /// <summary>Mensaje cuando NO hace falta tanquear (null si hace falta).</summary>
    public static string? MotivoSinTanqueo(bool requiereTanqueo, double autonomiaKm, double? rutaRestanteKm)
    {
        if (requiereTanqueo) return null;
        return rutaRestanteKm is { } restante
            ? $"La autonomía ({Redondear0(autonomiaKm)} km) cubre la ruta restante ({Redondear0(restante)} km)."
            : "La autonomía cubre la ruta.";
    }

    private static string Redondear0(double valor) =>
        Math.Round(valor, 0, MidpointRounding.AwayFromZero).ToString("0", System.Globalization.CultureInfo.InvariantCulture);
}
