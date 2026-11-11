namespace Smartank.Infrastructure.Autorizaciones.Adaptadores;

/// <summary>Configuracion de la API de la EDS (seccion "Eds": Eds__ApiBaseUrl, Eds__ApiKey).</summary>
public class clsEdsOptions
{
    public const string SECCION = "Eds";

    /// <summary>URL base de la API de la EDS. Sin ella la pre-autorizacion se confirma de forma simulada (no hay EDS piloto conectada).</summary>
    public string? ApiBaseUrl { get; set; }

    /// <summary>Llave de la API de la EDS (secreto).</summary>
    public string? ApiKey { get; set; }
}
