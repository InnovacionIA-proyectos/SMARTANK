namespace Smartank.Infrastructure.Autorizaciones.Adaptadores;

/// <summary>
/// Configuracion de WhatsApp (seccion "Whatsapp": Whatsapp__ApiUrl, Whatsapp__ApiKey, ...). La API es Evolution API: se envia
/// un POST a <see cref="ApiUrl"/> con el header <c>apikey</c>. Sin ApiUrl y ApiKey no se envia nada (la orden queda preparada).
/// </summary>
public class clsWhatsappOptions
{
    public const string SECCION = "Whatsapp";

    /// <summary>Endpoint completo de envio de texto de la API (no se conserva en el codigo: depende del despliegue).</summary>
    public string? ApiUrl { get; set; }

    /// <summary>Llave de la API (secreto).</summary>
    public string? ApiKey { get; set; }

    /// <summary>Retraso en milisegundos con el que la API simula "escribiendo" antes de enviar.</summary>
    public int DelayMs { get; set; } = 1200;

    /// <summary>
    /// Modo de pruebas: TODAS las ordenes se redirigen a <see cref="NumeroPrueba"/> en vez de al conductor. Nunca debe quedar
    /// activo en produccion: el host valida el arranque con <see cref="Validar"/>.
    /// </summary>
    public bool ModoPrueba { get; set; }

    /// <summary>Celular de pruebas (formato colombiano); solo se usa con <see cref="ModoPrueba"/>.</summary>
    public string? NumeroPrueba { get; set; }

    /// <summary>True si hay URL y llave: solo entonces se intenta enviar.</summary>
    public bool EstaConfigurado => !string.IsNullOrWhiteSpace(ApiUrl) && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>
    /// Valida la configuracion al arrancar. En produccion el modo de pruebas esta prohibido (desviaria las ordenes reales a un
    /// celular de pruebas); en cualquier entorno, el modo de pruebas exige un numero de pruebas.
    /// </summary>
    /// <exception cref="InvalidOperationException">La configuracion no es valida.</exception>
    public void Validar(bool esProduccion)
    {
        if (ModoPrueba && esProduccion)
            throw new InvalidOperationException(
                "Whatsapp__ModoPrueba esta activo en produccion: las ordenes de tanqueo se desviarian a un numero de pruebas. Desactivelo.");
        if (ModoPrueba && string.IsNullOrWhiteSpace(NumeroPrueba))
            throw new InvalidOperationException("Whatsapp__ModoPrueba esta activo pero falta Whatsapp__NumeroPrueba.");
    }
}
