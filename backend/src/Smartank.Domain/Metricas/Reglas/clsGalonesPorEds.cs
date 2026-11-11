namespace Smartank.Domain.Metricas.Reglas;

/// <summary>Volumen tanqueado en una estacion del catalogo, para el mapa de EDS.</summary>
public sealed record clsGalonesPorEds(
    Guid Id,
    string Nombre,
    double Lat,
    double Lng,
    string? Ruta,
    string? Proveedor,
    decimal? Precio,
    decimal Galones,
    int Tanqueos);
