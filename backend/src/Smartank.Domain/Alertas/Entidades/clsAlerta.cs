using Smartank.Domain.Enums;

namespace Smartank.Domain.Alertas.Entidades;

/// <summary>Alerta de control generada al conciliar un tanqueo o un consumo (tabla tblAlertas).</summary>
public class clsAlerta
{
    public Guid Id { get; set; }

    public TipoAlerta Tipo { get; set; }

    public Guid VehiculoId { get; set; }

    public Guid? EventoId { get; set; }

    public double? MagnitudDesviacion { get; set; }

    public EstadoAlerta Estado { get; set; } = EstadoAlerta.Nueva;

    public Guid? GestionadaPor { get; set; }

    /// <summary>Token de concurrencia optimista (rowversion). Lo genera la base; no se asigna a mano.</summary>
    public byte[] RowVersion { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
