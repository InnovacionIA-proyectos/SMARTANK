namespace Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;

/// <summary>Como termino un job. Un fallo nunca se propaga a los demas jobs ni tumba el proceso.</summary>
public enum EstadoJob
{
    /// <summary>Termino sin errores de registros.</summary>
    Ok,

    /// <summary>Termino pero algunos registros fallaron (detalle en el resultado).</summary>
    ConErrores,

    /// <summary>No se ejecuto porque falta configuracion o el servicio externo no esta disponible (aviso, no error fatal).</summary>
    Omitido,

    /// <summary>Lanzo una excepcion inesperada.</summary>
    Fallido,

    /// <summary>Lo interrumpio el apagado del proceso.</summary>
    Cancelado,
}
