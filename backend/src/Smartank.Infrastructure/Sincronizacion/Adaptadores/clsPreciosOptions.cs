namespace Smartank.Infrastructure.Sincronizacion.Adaptadores;

/// <summary>Configuracion de la sincronizacion de precios de EDS (seccion <c>Precios</c>; variables <c>Precios__*</c>).</summary>
public class clsPreciosOptions
{
    public const string SECCION = "Precios";

    /// <summary>Segundos de espera por solicitud a cada fuente (con reintentos del handler de resiliencia).</summary>
    public const int TIMEOUT_POR_DEFECTO_SEGUNDOS = 60;

    /// <summary>
    /// Reservado: el sistema original declaraba <c>PRECIOS_API_BASE_URL</c>/<c>PRECIOS_API_KEY</c> pero nunca las uso; los precios
    /// salen de las fuentes publicas de cada red. Se conservan para un proveedor propio de precios, hoy sin efecto.
    /// </summary>
    public string? ApiBaseUrl { get; set; }

    public string? ApiKey { get; set; }

    /// <summary>Servicio publico de puntos de Terpel (GET, JSON).</summary>
    public string TerpelUrl { get; set; } = "https://www.terpel.com/api/map_points/eds";

    /// <summary>Sitio publico de Distracom: el GET inicial de <c>/distraloc</c> entrega la cookie que exige el POST de estaciones.</summary>
    public string DistracomUrl { get; set; } = "https://www.distracom.com.co";

    public int TimeoutSegundos { get; set; } = TIMEOUT_POR_DEFECTO_SEGUNDOS;
}
