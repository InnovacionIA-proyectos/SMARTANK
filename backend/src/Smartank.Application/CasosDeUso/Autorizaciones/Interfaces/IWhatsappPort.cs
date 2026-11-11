namespace Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

/// <summary>Puerto hacia el servicio de WhatsApp (Evolution API) con el que se le envia la orden de tanqueo al conductor.</summary>
public interface IWhatsappPort
{
    /// <summary>True si hay URL y llave de la API: solo entonces se intenta enviar; si no, la orden queda preparada sin enviar.</summary>
    bool EstaConfigurado { get; }

    /// <summary>True si TODAS las ordenes deben redirigirse a <see cref="NumeroPrueba"/> en lugar del conductor.</summary>
    bool ModoPrueba { get; }

    /// <summary>Celular de pruebas (solo se usa con <see cref="ModoPrueba"/>), o null.</summary>
    string? NumeroPrueba { get; }

    /// <summary>
    /// Envia <paramref name="texto"/> al <paramref name="numero"/> (E.164, se normaliza a digitos) y devuelve el identificador del
    /// mensaje. Lanza <c>clsWhatsappEnvioException</c> si la red falla o la API responde con error.
    /// </summary>
    Task<string> EnviarAsync(string numero, string texto, CancellationToken ct);
}
