using Smartank.Domain.Enums;

namespace Smartank.Domain.Eventos.Reglas;

/// <summary>Insumos de la verificacion: lo que reporto el conductor frente a lo autorizado y a los precios de referencia.</summary>
/// <param name="GalonesCargados">Galones que el conductor declara haber cargado.</param>
/// <param name="NivelAntes">Nivel de tanque (galones) antes de cargar; null si no se tiene telemetria.</param>
/// <param name="NivelDespues">Nivel de tanque (galones) despues de cargar; null si no se tiene telemetria.</param>
/// <param name="GalonesAutorizados">Tope autorizado para la carga.</param>
/// <param name="PrecioEstacion">Precio por galon de la EDS donde se cargo; null si no se conoce.</param>
/// <param name="PrecioReferencia">Precio de la EDS mas barata de la misma ruta; null si no hay referencia.</param>
/// <param name="Tolerancia">Fraccion 0-1 por debajo de lo autorizado que aun cuenta como ejecucion completa.</param>
/// <param name="UmbralGalonFantasma">Fraccion de los galones cargados que puede faltar en el aumento de nivel antes de alertar.</param>
public sealed record clsDatosVerificacion(
    decimal GalonesCargados,
    decimal? NivelAntes,
    decimal? NivelDespues,
    decimal GalonesAutorizados,
    decimal? PrecioEstacion,
    decimal? PrecioReferencia,
    double Tolerancia,
    double UmbralGalonFantasma);

/// <summary>Alerta detectada: tipo y magnitud de la desviacion (galones o pesos por galon segun el tipo).</summary>
public readonly record struct clsAlertaDetectada(TipoAlerta Tipo, double Magnitud);

/// <summary>Resultado de verificar una carga: estado final de la autorizacion y las alertas que hay que crear.</summary>
public sealed record clsResultadoVerificacion(EstadoAutorizacion EstadoFinal, IReadOnlyList<clsAlertaDetectada> Alertas);

/// <summary>
/// Cruce de un evento de bitacora contra su autorizacion y la telemetria (port fiel de verificacion.ts). Dispara:
/// <c>DesviacionConsumoCarga</c> (lo cargado supera el aumento real de nivel), <c>EjecucionParcial</c> (se cargo menos de lo
/// autorizado mas alla de la tolerancia) y <c>EstacionCara</c> (precio por encima de la EDS mas barata de la ruta). El estado
/// final es EjecutadaParcial si hubo ejecucion parcial y Ejecutada en otro caso.
/// </summary>
public static class clsVerificacionCarga
{
    /// <summary>Evalua las tres reglas en el orden del origen: desviacion de carga, ejecucion parcial, estacion cara.</summary>
    public static clsResultadoVerificacion Evaluar(clsDatosVerificacion datos)
    {
        var alertas = new List<clsAlertaDetectada>();

        // 1) Desviacion de consumo (carga): el aumento real de nivel no corresponde a lo cargado.
        if (datos.NivelAntes is { } antes && datos.NivelDespues is { } despues)
        {
            var incremento = despues - antes;
            var faltante = datos.GalonesCargados - incremento;
            if (faltante > (decimal)datos.UmbralGalonFantasma * datos.GalonesCargados)
                alertas.Add(new clsAlertaDetectada(TipoAlerta.DesviacionConsumoCarga, Redondear2(faltante)));
        }

        // 2) Ejecucion parcial: se cargo menos de lo autorizado mas alla de la tolerancia.
        var minimoEsperado = datos.GalonesAutorizados * (1 - (decimal)datos.Tolerancia);
        var esParcial = datos.GalonesCargados < minimoEsperado;
        if (esParcial)
            alertas.Add(new clsAlertaDetectada(
                TipoAlerta.EjecucionParcial, Redondear2(datos.GalonesAutorizados - datos.GalonesCargados)));

        // 3) Estacion cara: precio por encima de la referencia (la mas barata de la ruta).
        if (datos.PrecioEstacion is { } precio && datos.PrecioReferencia is { } referencia && precio > referencia)
            alertas.Add(new clsAlertaDetectada(TipoAlerta.EstacionCara, (double)(precio - referencia)));

        var estadoFinal = esParcial ? EstadoAutorizacion.EjecutadaParcial : EstadoAutorizacion.Ejecutada;
        return new clsResultadoVerificacion(estadoFinal, alertas);
    }

    private static double Redondear2(decimal valor) => (double)Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
