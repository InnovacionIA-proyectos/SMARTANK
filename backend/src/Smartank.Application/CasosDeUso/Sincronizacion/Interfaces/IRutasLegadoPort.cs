using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Lectura (solo lectura) de las rutas calculadas por Valhalla y de los centros poblados DANE en el SQL Server legado.</summary>
public interface IRutasLegadoPort
{
    /// <summary>Calculo mas reciente por FO (operacion normal, con geometria) creado desde la fecha dada (hora de Colombia).</summary>
    Task<IReadOnlyList<clsRutaIndiceLegadoDto>> ListarIndiceAsync(DateTime desdeHoraColombia, CancellationToken ct = default);

    /// <summary>Geometria completa (WKT), kilometros y detalle de los calculos indicados. Pensado para lotes pequenos.</summary>
    Task<IReadOnlyList<clsRutaValhallaDto>> ObtenerGeometriasAsync(IReadOnlyCollection<long> valhallaIds, CancellationToken ct = default);

    /// <summary>Centros poblados DANE con coordenadas, por codigo normalizado a 12 digitos.</summary>
    Task<IReadOnlyDictionary<string, clsCentroDaneDto>> ListarCentrosDaneAsync(CancellationToken ct = default);
}
