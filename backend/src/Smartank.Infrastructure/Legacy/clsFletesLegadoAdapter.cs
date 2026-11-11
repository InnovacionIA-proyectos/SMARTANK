using System.Data;
using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Common;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Infrastructure.Legacy;

/// <summary>
/// Anticorrupcion de tblFOs (fletes). Traduce columnas, quita los ceros del numero de FO y convierte la fecha de despacho
/// (hora de Colombia en el legado) a UTC con <see cref="clsHoraColombia"/>.
/// </summary>
public sealed class clsFletesLegadoAdapter(clsLegadoConexion conexion) : IFletesLegadoPort
{
    private const string SQL = """
        SELECT ID, PLACACABEZOTE, PROGRAMACABEZOTE, PLACATRAILER, CEDULACONDUCTOR, TRANSPORTISTA,
               CODIGORUTA, NOMBRERUTA, CODIGOCIUDADORIGEN, CODIGOCIUDADDESTINO, NUMEROFO,
               PESOFO, GALONESEDS, GALONESCREDITO, GALONESCONTADO, GALONESRUTA, KMTSRUTA,
               FECHADESPACHO, GASTOSGENERADOS, GASTOSENVIADOSAP, CONSUMOENVIADOSAP, ENVIADOSAP, BORRADO
        FROM tblFOs
        WHERE FECHADESPACHO >= @desde
        ORDER BY FECHADESPACHO
        """;

    public async Task<IReadOnlyList<clsFleteLegadoDto>> ListarDesdeAsync(DateTime desdeHoraColombia, CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirFpsapAsync(ct);

        // DbType.DateTime explicito: con el datetime2 por defecto SQL Server convertiria la COLUMNA y no podria usar su indice.
        var parametros = new DynamicParameters();
        parametros.Add("desde", desdeHoraColombia, DbType.DateTime);

        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL, parametros, ct);
        return filas.Select(f => new clsFleteLegadoDto(
            f.Entero("ID"),
            f.Texto("PLACACABEZOTE") ?? string.Empty,
            f.Texto("PROGRAMACABEZOTE"),
            f.Texto("PLACATRAILER"),
            f.Texto("CEDULACONDUCTOR"),
            f.Texto("TRANSPORTISTA"),
            f.Texto("CODIGORUTA"),
            f.Texto("NOMBRERUTA"),
            f.Texto("CODIGOCIUDADORIGEN"),
            f.Texto("CODIGOCIUDADDESTINO"),
            clsTexto.StripCeros(f.Texto("NUMEROFO")),
            f.Decimal("PESOFO"),
            f.Decimal("GALONESEDS"),
            f.Decimal("GALONESCREDITO"),
            f.Decimal("GALONESCONTADO"),
            f.Decimal("GALONESRUTA"),
            f.Decimal("KMTSRUTA"),
            clsHoraColombia.AUtc(f.Fecha("FECHADESPACHO")),
            f.Booleano("GASTOSGENERADOS"),
            f.Booleano("GASTOSENVIADOSAP"),
            f.Booleano("CONSUMOENVIADOSAP"),
            f.Booleano("ENVIADOSAP"),
            f.Booleano("BORRADO"))).ToList();
    }
}
