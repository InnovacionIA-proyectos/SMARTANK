namespace Smartank.Api.Recomendacion;

/// <summary>Cuerpo opcional de la recomendacion: galones explicitos (si no vienen, se calculan para completar la ruta).</summary>
public class clsRecomendarTanqueoRequest
{
    public decimal? Galones { get; set; }
}
