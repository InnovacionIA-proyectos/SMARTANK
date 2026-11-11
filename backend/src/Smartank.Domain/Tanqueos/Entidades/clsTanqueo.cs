using Smartank.Domain.Enums;

namespace Smartank.Domain.Tanqueos.Entidades;

/// <summary>
/// Tanqueo del historial real de los programas de abastecimiento (tabla tblTanqueos), unico por (Fuente, FuenteId).
/// Distinto de <c>clsEventoTanqueo</c>, que es la bitacora del flujo de autorizacion.
/// </summary>
public class clsTanqueo
{
    public Guid Id { get; set; }

    public FuenteTanqueo Fuente { get; set; }

    /// <summary>Id del registro en la tabla de origen de la fuente.</summary>
    public long FuenteId { get; set; }

    /// <summary>Null cuando la placa no existe en la flota (el registro se conserva para no subestimar el consumo).</summary>
    public Guid? VehiculoId { get; set; }

    public string Placa { get; set; } = string.Empty;

    public DateTimeOffset Fecha { get; set; }

    public decimal? Galones { get; set; }

    public decimal? ValorGalon { get; set; }

    public decimal? ValorTotal { get; set; }

    public string? Producto { get; set; }

    public string? FamiliaProducto { get; set; }

    public string? Ciudad { get; set; }

    /// <summary>Nombre de la estacion tal como lo reporta la fuente (se cruza con tblEstaciones por nombre normalizado).</summary>
    public string? Estacion { get; set; }

    /// <summary>Odometro reportado por la fuente, en km.</summary>
    public double? Kilometraje { get; set; }

    public string? CedulaConductor { get; set; }

    public string? Factura { get; set; }

    public string? Referencia { get; set; }

    public string? CentroCosto { get; set; }

    public DateTimeOffset? SincronizadoAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
