using Smartank.Application.CasosDeUso.Bitacora.DTOs;

namespace Smartank.Application.CasosDeUso.Bitacora.Interfaces;

/// <summary>Lectura de lo que muestra la bitacora del conductor (sin seguimiento ni efectos).</summary>
public interface IBitacoraConsultas
{
    /// <summary>Datos de la orden (placa, manifiesto, ruta, estacion, galones, estado) o null si la autorizacion no existe.</summary>
    Task<clsBitacoraDto?> ObtenerAsync(Guid autorizacionId, CancellationToken ct);
}
