using System.Data;
using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Common;

namespace Smartank.Infrastructure.Legacy;

/// <summary>
/// Manifiestos cerrados: evento ZEND_TRIP con fecha real en el DW de operaciones (servidor Ebs, base DWCubosOperacionesEBS).
/// Se cruza EH_HDR por TRACKINGID (= manifiesto = numero de FO sin ceros) con su evento EH_EXPEV. El filtro de fecha descarta
/// el centinela de "sin cierre" y limita el volumen.
/// </summary>
public sealed class clsManifiestosCerradosLegadoAdapter(clsLegadoConexion conexion) : IManifiestosCerradosPort
{
    private const string SQL = """
        SELECT DISTINCT a.TRACKINGID
        FROM EH_HDR AS a
        INNER JOIN EH_EXPEV AS b ON b.EH_GUID = a.EH_GUID
        WHERE b.EVENT_CODE = 'ZEND_TRIP' AND b.EVENT_DATE >= @desde
        """;

    public async Task<IReadOnlySet<string>> ListarDesdeAsync(DateTime desdeHoraColombia, CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirEbsAsync(ct);

        var parametros = new DynamicParameters();
        parametros.Add("desde", desdeHoraColombia, DbType.DateTime);

        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL, parametros, ct);
        var manifiestos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var f in filas)
            if (clsTexto.StripCeros(f.Texto("TRACKINGID")) is { } manifiesto) manifiestos.Add(manifiesto);
        return manifiestos;
    }
}
