namespace Smartank.Application.CasosDeUso.Autorizaciones.DTOs;

/// <summary>Destinatario de la orden (nombre del conductor y celular en formato E.164).</summary>
public sealed record clsDestinatarioDto(string Nombre, string Telefono);

/// <summary>Contenido de la orden de tanqueo enviada.</summary>
public sealed record clsOrdenTanqueoDto(
    string? Placa,
    string? Manifiesto,
    string? Ruta,
    string Estacion,
    decimal GalonesAutorizados,
    string? MapaUrl);

/// <summary>Resultado del envio por WhatsApp. Si <see cref="Enviado"/> es false, <see cref="Motivo"/> explica por que (la peticion no falla).</summary>
public sealed record clsWhatsappResultadoDto(bool Enviado, string? Sid, string? Motivo);

/// <summary>Respuesta de notificar al conductor (NotificarResponse del origen mas autorizacion_id y la orden).</summary>
public sealed record clsNotificarDto(
    Guid AutorizacionId,
    clsDestinatarioDto Destinatario,
    clsOrdenTanqueoDto Orden,
    string BitacoraUrl,
    clsWhatsappResultadoDto Whatsapp);
