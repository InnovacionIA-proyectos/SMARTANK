using Smartank.Domain.Enums;

namespace Smartank.Domain.Alertas.Reglas;

/// <summary>
/// Ciclo de vida de una alerta en la bandeja de Control de Flota: Nueva -> EnRevision -> Gestionada o Descartada.
/// Se puede saltar EnRevision (una alerta evidente se cierra de una vez), pero un estado final no se reabre y nada
/// vuelve a Nueva: la bandeja usa ese estado para saber que nadie la ha visto.
/// </summary>
public static class clsTransicionesAlerta
{
    /// <summary>Estados a los que se puede pasar desde <paramref name="actual"/>. Vacio si el estado es final.</summary>
    public static IReadOnlyList<EstadoAlerta> Destinos(EstadoAlerta actual) => actual switch
    {
        EstadoAlerta.Nueva => [EstadoAlerta.EnRevision, EstadoAlerta.Gestionada, EstadoAlerta.Descartada],
        EstadoAlerta.EnRevision => [EstadoAlerta.Gestionada, EstadoAlerta.Descartada],
        _ => [],
    };

    public static bool PuedePasar(EstadoAlerta actual, EstadoAlerta destino) => Destinos(actual).Contains(destino);

    /// <summary>Estados finales: una vez alcanzados la alerta queda cerrada.</summary>
    public static bool EsFinal(EstadoAlerta estado) => Destinos(estado).Count == 0;
}
