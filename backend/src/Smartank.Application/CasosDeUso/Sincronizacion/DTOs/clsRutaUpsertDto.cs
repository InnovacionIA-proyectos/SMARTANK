using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Ruta lista para escribir (clave Fo). La polilinea ya va como JSON <c>[[lat,lng],...]</c>; origen y destino los deriva un trigger de la base.</summary>
public sealed record clsRutaUpsertDto(
    string Fo,
    Guid? ViajeId,
    FuenteRuta Fuente,
    long? ValhallaId,
    double? Kilometros,
    int NumPuntos,
    int? NumPuntosOriginal,
    string PolilineaJson,
    string? DetalleKmJson,
    DateTimeOffset? FechaCalculo);
