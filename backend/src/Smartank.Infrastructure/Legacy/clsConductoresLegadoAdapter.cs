using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

namespace Smartank.Infrastructure.Legacy;

/// <summary>
/// Anticorrupcion de tblConductores y de los celulares de la app movil. El registro de usuarios de la app vive en la MISMA
/// instancia que Valhalla (base dbSolucionesTI), por eso se consulta con nombre de tres partes desde la conexion Fpsap.
/// </summary>
public sealed class clsConductoresLegadoAdapter(clsLegadoConexion conexion) : IConductoresLegadoPort
{
    private const string SQL_CONDUCTORES = """
        SELECT ID, CEDULA, NOMBRES, ACTIVO, UTILIZARTARJETAPREPAGO
        FROM tblConductores
        ORDER BY NOMBRES
        """;

    // Por documento puede haber varias filas (una por placa/registro): se toma el celular del Activo mas reciente.
    private const string SQL_CELULARES = """
        SELECT NRODOCUMENTO, CELULAR FROM (
            SELECT NRODOCUMENTO, CELULAR,
                   ROW_NUMBER() OVER (
                       PARTITION BY NRODOCUMENTO
                       ORDER BY CASE WHEN ESTADO = 'Activo' THEN 0 ELSE 1 END,
                                FECHAULTIMOINGRESO DESC
                   ) AS rn
            FROM dbSolucionesTI.dbo.tblRegistroUsuariosAplMovil
            WHERE CELULAR IS NOT NULL AND LTRIM(RTRIM(CELULAR)) <> ''
              AND NRODOCUMENTO IS NOT NULL AND LTRIM(RTRIM(NRODOCUMENTO)) <> ''
        ) t WHERE rn = 1
        """;

    public async Task<IReadOnlyList<clsConductorLegadoDto>> ListarAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirFpsapAsync(ct);
        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL_CONDUCTORES, null, ct);
        return filas.Select(f => new clsConductorLegadoDto(
            f.Entero("ID"),
            f.Texto("CEDULA") ?? string.Empty,
            f.Texto("NOMBRES") ?? string.Empty,
            f.Booleano("ACTIVO"),
            f.Booleano("UTILIZARTARJETAPREPAGO"))).ToList();
    }

    public async Task<IReadOnlyDictionary<string, string>> ListarCelularesAplMovilAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirFpsapAsync(ct);
        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL_CELULARES, null, ct);
        var mapa = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in filas)
            if (f.Texto("NRODOCUMENTO") is { } documento && f.Texto("CELULAR") is { } celular)
                mapa[documento] = celular;
        return mapa;
    }
}
