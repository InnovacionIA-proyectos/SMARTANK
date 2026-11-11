using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Fuentes publicas de precios de EDS de cada red (Terpel y Distracom). Un fallo de red lanza una excepcion que el caso de uso aisla por fuente.</summary>
public interface IPreciosEdsPort
{
    Task<IReadOnlyList<clsPrecioEdsDto>> ObtenerTerpelAsync(CancellationToken ct = default);

    Task<IReadOnlyList<clsPrecioEdsDto>> ObtenerDistracomAsync(CancellationToken ct = default);
}
