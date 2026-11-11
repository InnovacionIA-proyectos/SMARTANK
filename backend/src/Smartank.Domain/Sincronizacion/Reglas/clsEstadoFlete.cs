using Smartank.Domain.Enums;

namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Estado del viaje derivado de las banderas del flete (FO) del legado.</summary>
public static class clsEstadoFlete
{
    /// <summary>
    /// BORRADO -> Descartado; cerrado por SAP (evento ZEND_TRIP con fecha real) -> Cerrado; despacho ya llegado
    /// (&lt;= ahora) -> EnRuta; despacho a futuro -> Programado. Sin fecha de despacho no se puede considerar en ruta.
    /// El cierre lo decide SAP, no la fecha.
    /// </summary>
    public static EstadoViaje Derivar(bool borrado, DateTimeOffset? fechaDespacho, bool cerradoPorSap, DateTimeOffset ahora)
    {
        if (borrado) return EstadoViaje.Descartado;
        if (cerradoPorSap) return EstadoViaje.Cerrado;
        if (fechaDespacho is null) return EstadoViaje.Programado;
        return fechaDespacho.Value <= ahora ? EstadoViaje.EnRuta : EstadoViaje.Programado;
    }
}
