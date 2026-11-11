using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Viajes.Interfaces;

/// <summary>Viaje con su ruta (polilinea), la posicion del vehiculo y los datos para calcular su autonomia actual.</summary>
public sealed record clsViajeRutaFila(
    Guid Id,
    string Ruta,
    EstadoViaje Estado,
    string Placa,
    double? VehiculoLat,
    double? VehiculoLng,
    DateTimeOffset? VehiculoUbicacionAt,
    decimal? NivelTanqueActual,
    double? RendimientoHistorico,
    double? RendimientoEsperado,
    double ToleranciaMotor,
    double? RutaKm,
    int? NumPuntos,
    double[][]? Polilinea);
