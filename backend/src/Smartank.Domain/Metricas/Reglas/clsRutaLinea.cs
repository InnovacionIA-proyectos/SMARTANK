namespace Smartank.Domain.Metricas.Reglas;

/// <summary>Ruta como segmento origen-destino (extremos reales de la polilinea) para el mapa de rutas.</summary>
public sealed record clsRutaLinea(
    string? Ruta,
    double OrigenLat,
    double OrigenLng,
    double DestinoLat,
    double DestinoLng);
