using Smartank.Domain.Enums;

namespace Smartank.Domain.Autorizaciones.Reglas;

/// <summary>
/// Ciclo de vida de una autorizacion de tanqueo:
/// <c>Propuesta -> Autorizada | Rechazada | Expirada</c>; <c>Autorizada -> Ejecutada | EjecutadaParcial | Expirada</c>.
/// Ejecutada, EjecutadaParcial, Expirada y Rechazada son finales: las no ejecutadas se conservan, nunca se reabren.
/// </summary>
public static class clsMaquinaEstadosAutorizacion
{
    private static readonly IReadOnlyDictionary<EstadoAutorizacion, EstadoAutorizacion[]> TRANSICIONES =
        new Dictionary<EstadoAutorizacion, EstadoAutorizacion[]>
        {
            [EstadoAutorizacion.Propuesta] =
                [EstadoAutorizacion.Autorizada, EstadoAutorizacion.Rechazada, EstadoAutorizacion.Expirada],
            [EstadoAutorizacion.Autorizada] =
                [EstadoAutorizacion.Ejecutada, EstadoAutorizacion.EjecutadaParcial, EstadoAutorizacion.Expirada],
            [EstadoAutorizacion.Ejecutada] = [],
            [EstadoAutorizacion.EjecutadaParcial] = [],
            [EstadoAutorizacion.Expirada] = [],
            [EstadoAutorizacion.Rechazada] = [],
        };

    /// <summary>Estados a los que se puede pasar desde <paramref name="desde"/> (vacio si es final).</summary>
    public static IReadOnlyList<EstadoAutorizacion> TransicionesDesde(EstadoAutorizacion desde) => TRANSICIONES[desde];

    /// <summary>True si <paramref name="de"/> -&gt; <paramref name="a"/> es una transicion valida.</summary>
    public static bool PuedeTransicionar(EstadoAutorizacion de, EstadoAutorizacion a) =>
        Array.IndexOf(TRANSICIONES[de], a) >= 0;

    /// <summary>True si el estado ya no admite mas cambios.</summary>
    public static bool EsFinal(EstadoAutorizacion estado) => TRANSICIONES[estado].Length == 0;

    /// <summary>
    /// True si la autorizacion ya fue verificada (el conductor cargo y se cruzo contra lo autorizado): Ejecutada o EjecutadaParcial.
    /// </summary>
    public static bool FueEjecutada(EstadoAutorizacion estado) =>
        estado is EstadoAutorizacion.Ejecutada or EstadoAutorizacion.EjecutadaParcial;
}
