using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Escritura masiva de viajes sincronizados desde los fletes del legado y lectura de lo necesario para las rutas.</summary>
public interface IViajesSincronizacionRepository
{
    /// <summary>Upsert por FoId; conserva lo que la aplicacion calcula (despachador, autonomia estimada).</summary>
    Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsViajeUpsertDto> viajes, CancellationToken ct = default);

    /// <summary>Viajes con numero de FO, con el id de Valhalla de su ruta almacenada (si la tienen).</summary>
    Task<IReadOnlyList<clsViajeParaRutaDto>> ListarParaRutasAsync(CancellationToken ct = default);
}
