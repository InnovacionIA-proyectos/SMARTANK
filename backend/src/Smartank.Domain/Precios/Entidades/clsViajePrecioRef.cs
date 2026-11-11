namespace Smartank.Domain.Precios.Entidades;

/// <summary>
/// Precios de referencia de un viaje (tabla tblViajePreciosRef): ACPM minimo y maximo entre las EDS cercanas a su ruta.
/// La llave es el propio <see cref="ViajeId"/> (una fila por viaje).
/// </summary>
public class clsViajePrecioRef
{
    public Guid ViajeId { get; set; }

    /// <summary>Precio de la EDS de ACPM mas barata cerca de la ruta.</summary>
    public decimal? AcpmMin { get; set; }

    /// <summary>Precio de la EDS de ACPM mas cara cerca de la ruta.</summary>
    public decimal? AcpmMax { get; set; }

    /// <summary>Cuantas EDS de ACPM quedaron dentro del umbral de cercania.</summary>
    public int EdsCerca { get; set; }

    public DateTimeOffset CalculadoAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
