namespace Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

/// <summary>Pre-autorizacion de carga que se le envia a la EDS: quien (placa), cuanto y cuando.</summary>
public sealed record clsPreautorizacionEdsSolicitud(
    string Placa,
    decimal GalonesMax,
    DateTimeOffset FechaInicio,
    DateTimeOffset FechaFin);

/// <summary>Confirmacion de la EDS. <see cref="Simulado"/> marca las respuestas generadas localmente por no haber EDS conectada.</summary>
public sealed record clsPreautorizacionEdsResultado(bool Ok, string? Referencia, bool Simulado, DateTimeOffset RecibidoAt);

/// <summary>Puerto hacia la API de la EDS (Terpel / Distracom) para pre-autorizar la carga en el surtidor.</summary>
public interface IEdsPort
{
    Task<clsPreautorizacionEdsResultado> RegistrarPreautorizacionAsync(clsPreautorizacionEdsSolicitud solicitud, CancellationToken ct);
}
