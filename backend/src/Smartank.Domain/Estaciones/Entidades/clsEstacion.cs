namespace Smartank.Domain.Estaciones.Entidades;

/// <summary>Estacion de servicio (EDS) del catalogo (tabla tblEstaciones), con coordenadas, producto y precios reales.</summary>
public class clsEstacion
{
    public Guid Id { get; set; }

    /// <summary>Id de la EDS en la fuente (Terpel "EDS####", Distracom entero).</summary>
    public string? CodigoExterno { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Columna calculada por la base: nombre en minusculas y sin espacios laterales. Solo lectura.</summary>
    public string? NombreNormalizado { get; private set; }

    public string? Ruta { get; set; }

    public double? Lat { get; set; }

    public double? Lng { get; set; }

    /// <summary>Red de abastecimiento: "terpel", "distracom" o "syt" (estacion propia).</summary>
    public string? Proveedor { get; set; }

    /// <summary>Combustibles que vende la EDS (ACPM, corriente, extra, GNV...); texto libre.</summary>
    public string? Producto { get; set; }

    /// <summary>Precio vigente por galon; replica <see cref="PrecioAcpm"/> (combustible de la flota). Null = sin dato.</summary>
    public decimal? PrecioGalonActual { get; set; }

    public decimal? PrecioAcpm { get; set; }

    public decimal? PrecioCorriente { get; set; }

    public DateTimeOffset? PrecioActualizadoAt { get; set; }

    public bool SoportaPreautorizacion { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
