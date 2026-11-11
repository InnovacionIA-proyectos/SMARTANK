namespace Smartank.Application.CasosDeUso.Bitacora.Interfaces;

/// <summary>
/// Token firmado que liga el enlace de WhatsApp a UNA autorizacion: el conductor reporta su carga sin cuenta de usuario.
/// A diferencia del manifiesto del origen (predecible), el token no se puede adivinar ni falsificar.
/// </summary>
public interface IBitacoraTokenPort
{
    /// <summary>Emite un token para la autorizacion, con la vigencia configurada (Bitacora__ExpiraHoras).</summary>
    string Emitir(Guid autorizacionId);

    /// <summary>
    /// Valida firma, vigencia y proposito y devuelve el id de la autorizacion. Lanza <c>clsUnauthorizedException</c>
    /// (BITACORA_TOKEN_INVALIDO o BITACORA_TOKEN_EXPIRADO) si el token no sirve.
    /// </summary>
    Guid Validar(string token);
}
