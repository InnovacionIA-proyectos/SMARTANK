namespace Smartank.Domain.Metricas.Reglas;

/// <summary>
/// Un tanqueo REAL de ACPM/diesel ya ligado a su contexto: el viaje vigente del vehiculo en esa fecha (y su ruta), la
/// referencia de precio de esa ruta (EDS de ACPM mas barata y mas cara cerca de la polilinea) y, si se pudo casar por
/// nombre, la estacion de nuestro catalogo (para ubicarla en el mapa). Es la entrada de <see cref="clsCalculoRecuperacion"/>.
/// </summary>
public sealed record clsTanqueoMetrica(
    Guid Id,
    string? Proveedor,
    string Placa,
    DateTimeOffset Fecha,
    decimal? Galones,
    decimal? PrecioPagado,
    decimal? ValorTotal,
    string? Estacion,
    string? Ciudad,
    string? Ruta,
    decimal? AcpmMin,
    decimal? AcpmMax,
    clsEstacionCatalogo? EstacionCatalogo,
    clsExtremosRuta? ExtremosRuta);

/// <summary>Estacion del catalogo propio a la que se casa un tanqueo del historial.</summary>
public sealed record clsEstacionCatalogo(
    Guid Id,
    string Nombre,
    double Lat,
    double Lng,
    string? Proveedor,
    decimal? PrecioAcpm);

/// <summary>Primer y ultimo punto de la polilinea de la ruta del viaje al que se liga el tanqueo.</summary>
public sealed record clsExtremosRuta(double OrigenLat, double OrigenLng, double DestinoLat, double DestinoLng);
