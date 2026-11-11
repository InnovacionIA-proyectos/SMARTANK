using System.Data;
using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Common;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Infrastructure.Legacy;

/// <summary>
/// Anticorrupcion de las rutas calculadas por Valhalla (dbSolucionesTI.dbo.tblLogPeticionesValhalla) y de los centros poblados
/// DANE (tblSAPDANE). Ambas viven en la misma instancia que FPSAP y se consultan con nombre de tres partes. La geometria se
/// pide COMPLETA con STAsText(); NUNCA Geom.Reduce(), que devuelve un MULTILINESTRING con los tramos desordenados.
/// </summary>
public sealed class clsRutasLegadoAdapter(clsLegadoConexion conexion) : IRutasLegadoPort
{
    private const string SQL_INDICE = """
        SELECT FO, MAX(ID) AS ID
        FROM dbSolucionesTI.dbo.tblLogPeticionesValhalla
        WHERE FECHACREACION >= @desde AND TIPOOPERACION = 'N' AND Geom IS NOT NULL AND FO <> ''
        GROUP BY FO
        """;

    private const string SQL_GEOMETRIAS = """
        SELECT ID, FO,
               ROUND(Geom.STLength() / 1000.0, 2) AS KM,
               Geom.STNumPoints() AS ORIG,
               Geom.STAsText() AS WKT,
               CAST(JSONRESPUESTA AS nvarchar(max)) AS JSONR,
               FECHACREACION AS FECHA
        FROM dbSolucionesTI.dbo.tblLogPeticionesValhalla
        WHERE ID IN @ids
        """;

    private const string SQL_DANE = """
        SELECT CODIGOCENTROPOBLADO AS COD, LATITUD AS LAT, LONGITUD AS LNG
        FROM dbSolucionesTI.dbo.tblSAPDANE
        WHERE LATITUD IS NOT NULL AND LONGITUD IS NOT NULL
        """;

    public async Task<IReadOnlyList<clsRutaIndiceLegadoDto>> ListarIndiceAsync(DateTime desdeHoraColombia, CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirFpsapAsync(ct);
        var parametros = new DynamicParameters();
        parametros.Add("desde", desdeHoraColombia, DbType.DateTime);
        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL_INDICE, parametros, ct);

        // El FO se guarda sin ceros (como el de los viajes); si dos grafias colapsan en el mismo FO gana el calculo mas reciente.
        var mayor = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var f in filas)
        {
            if (clsTexto.StripCeros(f.Texto("FO")) is not { } fo) continue;
            var id = f.Entero("ID");
            if (!mayor.TryGetValue(fo, out var previo) || id > previo) mayor[fo] = id;
        }
        return mayor.Select(p => new clsRutaIndiceLegadoDto(p.Key, p.Value)).ToList();
    }

    public async Task<IReadOnlyList<clsRutaValhallaDto>> ObtenerGeometriasAsync(
        IReadOnlyCollection<long> valhallaIds, CancellationToken ct = default)
    {
        if (valhallaIds.Count == 0) return [];

        await using var cn = await conexion.AbrirFpsapAsync(ct);
        // Dapper expande "IN @ids" en un parametro por elemento: la consulta sigue parametrizada.
        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL_GEOMETRIAS, new { ids = valhallaIds.ToArray() }, ct);
        return filas.Select(f => new clsRutaValhallaDto(
            f.Entero("ID"),
            clsTexto.StripCeros(f.Texto("FO")) ?? string.Empty,
            f.Doble("KM"),
            (int)f.Entero("ORIG"),
            f.Texto("WKT"),
            f.Texto("JSONR"),
            clsHoraColombia.AUtc(f.Fecha("FECHA")))).ToList();
    }

    public async Task<IReadOnlyDictionary<string, clsCentroDaneDto>> ListarCentrosDaneAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirFpsapAsync(ct);
        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL_DANE, null, ct);
        var centros = new Dictionary<string, clsCentroDaneDto>(StringComparer.Ordinal);
        foreach (var f in filas)
        {
            // Left-pad a 12 digitos (sin recortar ceros): da coincidencia exacta con CODIGOCIUDADORIGEN/DESTINO del FO.
            var codigo = clsTexto.NormalizarCodigoDane(f.Texto("COD"));
            if (codigo is not null && f.Doble("LAT") is { } lat && f.Doble("LNG") is { } lng)
                centros[codigo] = new clsCentroDaneDto(codigo, lat, lng);
        }
        return centros;
    }
}
