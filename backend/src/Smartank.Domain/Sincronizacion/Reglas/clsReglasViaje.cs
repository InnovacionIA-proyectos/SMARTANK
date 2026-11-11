using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Reglas puras que arman un viaje a partir de un flete del legado.</summary>
public static class clsReglasViaje
{
    /// <summary>El manifiesto del viaje es el numero de FO sin ceros a la izquierda.</summary>
    public static string? Manifiesto(string? numeroFo) => clsTexto.StripCeros(numeroFo);

    /// <summary>La ruta del viaje es NOT NULL: NOMBRERUTA (casi siempre vacio en los programados) y, si no, CODIGORUTA.</summary>
    public static string ResolverRuta(string? nombreRuta, string? codigoRuta) =>
        clsTexto.NullifyBlank(nombreRuta) ?? clsTexto.NullifyBlank(codigoRuta) ?? string.Empty;

    /// <summary>Los programados aun no han iniciado (sin InicioAt); el resto usa la fecha de despacho.</summary>
    public static DateTimeOffset? InicioAt(EstadoViaje estado, DateTimeOffset? fechaDespacho) =>
        estado == EstadoViaje.Programado ? null : fechaDespacho;
}
