namespace Smartank.Domain.Enums;

// Enums del dominio. Los miembros van en PascalCase (convencion C#); en JSON y en la base de datos se
// representan como texto snake_case (EnRuta -> "en_ruta") mediante clsSnakeCase, de modo que el contrato
// del API y los CHECK de ddl.sql usan los mismos valores.

/// <summary>Rol de un usuario de la aplicacion.</summary>
public enum Rol
{
    Despacho,
    ControlFlota,
    Finanzas,
    Admin,
}

/// <summary>Estado del viaje (flete). Cerrado lo determina SAP (evento ZEND_TRIP); EnRuta/Programado dependen de la fecha de despacho.</summary>
public enum EstadoViaje
{
    Programado,
    EnRuta,
    Cerrado,
    Descartado,
}

/// <summary>Ciclo de vida de una autorizacion de tanqueo. Las no ejecutadas se conservan (Expirada/Rechazada), nunca se borran.</summary>
public enum EstadoAutorizacion
{
    Propuesta,
    Autorizada,
    Ejecutada,
    EjecutadaParcial,
    Expirada,
    Rechazada,
}

public enum TipoAlerta
{
    DesviacionConsumoCarga,
    DesviacionConsumo,
    EstacionCara,
    EjecucionParcial,
}

public enum EstadoAlerta
{
    Nueva,
    EnRevision,
    Gestionada,
    Descartada,
}

/// <summary>Origen de la polilinea de una ruta: Valhalla (detallada) o Dane (recta origen-destino por centros poblados).</summary>
public enum FuenteRuta
{
    Valhalla,
    Dane,
}

/// <summary>Red de abastecimiento de donde proviene un tanqueo del historial.</summary>
public enum FuenteTanqueo
{
    Distracom,
    Terpel,
}
