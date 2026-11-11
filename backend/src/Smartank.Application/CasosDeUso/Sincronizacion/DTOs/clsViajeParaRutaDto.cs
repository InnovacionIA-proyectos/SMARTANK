namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Viaje con FO y lo que ya tiene almacenado de su ruta, para decidir en modo incremental si hay que (re)calcularla.</summary>
public sealed record clsViajeParaRutaDto(
    Guid ViajeId,
    string NumeroFo,
    string? CiudadOrigen,
    string? CiudadDestino,
    long? ValhallaIdAlmacenado,
    bool TieneRuta);
