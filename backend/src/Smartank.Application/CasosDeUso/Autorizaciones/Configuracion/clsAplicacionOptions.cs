namespace Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;

/// <summary>Datos publicos de la aplicacion (seccion "App", variable App__UrlPublica).</summary>
public class clsAplicacionOptions
{
    public const string SECCION = "App";

    /// <summary>URL publica de la SPA, sin barra final (con ella se arma el enlace de la bitacora del conductor). Obligatoria para notificar.</summary>
    public string? UrlPublica { get; set; }
}
