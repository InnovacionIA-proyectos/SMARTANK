namespace Smartank.Domain.Viajes.Reglas;

/// <summary>
/// Un desplazamiento VACIO es un viaje sin carga (reposicionamiento del cabezote): su manifiesto interno empieza con
/// "32" y mide exactamente 10 caracteres. Cualquier otro manifiesto, o ninguno, es un desplazamiento CARGADO.
/// </summary>
public static class clsDesplazamiento
{
    public const string PREFIJO_MANIFIESTO_VACIO = "32";
    public const int LONGITUD_MANIFIESTO_VACIO = 10;

    public static bool EsVacio(string? manifiesto) =>
        manifiesto is not null
        && manifiesto.Length == LONGITUD_MANIFIESTO_VACIO
        && manifiesto.StartsWith(PREFIJO_MANIFIESTO_VACIO, StringComparison.Ordinal);
}
