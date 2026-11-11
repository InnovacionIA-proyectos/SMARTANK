namespace Smartank.Api.Eventos;

/// <summary>
/// Reporte del conductor con el token de su enlace. Todo es opcional a nivel de binding: la validacion (y el contrato de error
/// comun) la hace el caso de uso.
/// </summary>
public class clsRegistrarEventoRequest
{
    public string? Token { get; set; }

    public decimal? GalonesCargados { get; set; }

    public decimal? PrecioPagado { get; set; }

    public string? FotoSurtidorUrl { get; set; }

    public double? OdometroEvento { get; set; }

    public decimal? NivelAntes { get; set; }

    public decimal? NivelDespues { get; set; }
}
