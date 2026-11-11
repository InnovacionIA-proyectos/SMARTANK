namespace Smartank.Application.Common.Configuracion;

/// <summary>
/// Parametros de negocio con defaults, ligados a la seccion de configuracion "ParametrosNegocio"
/// (variables de entorno ParametrosNegocio__ToleranciaAutonomiaDefault, etc.). Se inyectan con IOptions.
/// </summary>
public class clsParametrosNegocio
{
    public const string SECCION = "ParametrosNegocio";

    /// <summary>Margen de tolerancia aplicado hacia abajo a la autonomia (0.02 = 2 %).</summary>
    public double ToleranciaAutonomiaDefault { get; set; } = 0.02;

    /// <summary>Fraccion de faltante entre galones cargados y aumento real de nivel que dispara la alerta de desviacion de consumo por carga.</summary>
    public double UmbralGalonFantasma { get; set; } = 0.05;

    /// <summary>Desviacion de consumo vs. esperado que dispara la alerta de extraccion.</summary>
    public double UmbralDesviacionConsumo { get; set; } = 0.10;

    /// <summary>Rendimiento (km/galon) cuando el vehiculo no tiene historico ni esperado (el maestro de flota no lo trae).</summary>
    public double RendimientoDefaultKmGal { get; set; } = 8;

    /// <summary>Galones a autorizar cuando no se puede calcular el deficit de la ruta.</summary>
    public double DefaultGalonesAutorizacion { get; set; } = 40;

    /// <summary>Antiguedad maxima, en horas, de la ultima lectura de nivel de tanque para calcular la autonomia (168 = 7 dias). Una lectura mas vieja se rechaza con 409 TELEMETRIA_OBSOLETA.</summary>
    public double NivelMaxAntiguedadHoras { get; set; } = 168;
}
