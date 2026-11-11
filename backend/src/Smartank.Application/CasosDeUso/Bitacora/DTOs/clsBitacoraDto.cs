using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Bitacora.DTOs;

/// <summary>Lo que el conductor ve al abrir el enlace de su bitacora: la orden de tanqueo y si ya la reporto.</summary>
/// <param name="AutorizacionId">Autorizacion a la que esta ligado el token.</param>
/// <param name="Placa">Placa del cabezote.</param>
/// <param name="Manifiesto">Manifiesto del viaje, si lo tiene.</param>
/// <param name="Ruta">Ruta del viaje.</param>
/// <param name="EstacionNombre">EDS autorizada.</param>
/// <param name="GalonesMax">Galones autorizados como maximo.</param>
/// <param name="Estado">Estado de la autorizacion (solo se puede reportar si esta autorizada).</param>
/// <param name="YaRegistrada">True si ya existe el evento de bitacora de esta autorizacion.</param>
/// <param name="VenceAt">Fin de la ventana de la autorizacion: pasada esa hora ya no se puede reportar.</param>
public sealed record clsBitacoraDto(
    Guid AutorizacionId,
    string Placa,
    string? Manifiesto,
    string? Ruta,
    string EstacionNombre,
    decimal GalonesMax,
    EstadoAutorizacion Estado,
    bool YaRegistrada,
    DateTimeOffset VenceAt);
