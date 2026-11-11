namespace Smartank.Api.Common.Auth;

/// <summary>Secreto de servicio (variable Servicio__Secreto) con el que se autentican los llamadores maquina a maquina, como la ingesta de telemetria.</summary>
public class clsServicioOptions
{
    public const string SECCION = "Servicio";

    /// <summary>Secreto compartido; sin el, los endpoints de servicio responden 503. Nunca en el repo.</summary>
    public string? Secreto { get; set; }
}
