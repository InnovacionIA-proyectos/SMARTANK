namespace Smartank.Api.Autorizaciones;

/// <summary>
/// Cuerpo para crear una autorizacion (propuesta) a mano. Todo es opcional a nivel de binding: la validacion (y su mensaje de
/// error con el contrato comun) la hace el caso de uso.
/// </summary>
public class clsCrearAutorizacionRequest
{
    public Guid? ViajeId { get; set; }

    public Guid? VehiculoId { get; set; }

    public Guid? EstacionId { get; set; }

    public decimal? GalonesMax { get; set; }

    public decimal? PrecioTecho { get; set; }

    public decimal? DeltaAhorroEstimado { get; set; }

    public DateTimeOffset? FechaInicio { get; set; }

    public DateTimeOffset? FechaFin { get; set; }
}
