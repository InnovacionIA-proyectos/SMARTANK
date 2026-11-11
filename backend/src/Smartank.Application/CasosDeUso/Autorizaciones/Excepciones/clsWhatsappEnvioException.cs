using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Autorizaciones.Excepciones;

/// <summary>
/// Fallo al enviar un mensaje de WhatsApp (red caida, tiempo agotado o error de la API). El caso de uso de notificar la
/// captura y la reporta en el resultado sin fallar la peticion; si escapara, se traduce a 503.
/// </summary>
public class clsWhatsappEnvioException(string message) : clsServiceUnavailableException("WHATSAPP_ENVIO_FALLIDO", message);
