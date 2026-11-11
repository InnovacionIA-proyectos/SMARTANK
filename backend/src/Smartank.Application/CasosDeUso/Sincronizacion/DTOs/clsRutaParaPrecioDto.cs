using Smartank.Domain.Geo;

namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Viaje con la polilinea de su ruta (al menos 2 puntos), para calcular las EDS cercanas.</summary>
public sealed record clsRutaParaPrecioDto(Guid ViajeId, IReadOnlyList<clsCoordenada> Polilinea);
