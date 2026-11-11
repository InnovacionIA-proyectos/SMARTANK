using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Viajes.Interfaces;

/// <summary>
/// Fila del listado de viajes tal como la lee el repositorio: SOLO las columnas que usan los paneles (no la polilinea
/// ni el resto de la entidad). La autonomia actual se calcula en Application con los datos de consumo del vehiculo.
/// </summary>
public sealed record clsViajeListadoFila(
    Guid Id,
    Guid VehiculoId,
    string Ruta,
    string? NombreRuta,
    string? CodigoRuta,
    EstadoViaje Estado,
    double? AutonomiaEstimadaKm,
    string? Manifiesto,
    DateTimeOffset? InicioAt,
    DateTimeOffset? FechaDespacho,
    string Placa,
    string TipoMotor,
    decimal? NivelTanqueActual,
    decimal? CapacidadTanqueGal,
    DateTimeOffset? UbicacionAt,
    DateTimeOffset? NivelAt,
    double? VehiculoLat,
    double? VehiculoLng,
    double? RendimientoHistorico,
    double? RendimientoEsperado,
    double ToleranciaMotor,
    double? RutaKm,
    double? OrigenLat,
    double? OrigenLng,
    double? DestinoLat,
    double? DestinoLng,
    bool RecomendacionActiva);
