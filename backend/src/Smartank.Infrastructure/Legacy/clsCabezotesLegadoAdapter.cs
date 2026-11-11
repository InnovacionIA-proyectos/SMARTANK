using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

namespace Smartank.Infrastructure.Legacy;

/// <summary>Anticorrupcion del maestro tblCabezotes (columnas en MAYUSCULAS) hacia el DTO propio. Solo lectura.</summary>
public sealed class clsCabezotesLegadoAdapter(clsLegadoConexion conexion) : ICabezotesLegadoPort
{
    private const string SQL = """
        SELECT ID, PLACA, TIPOMOTOR,
               GALONESCAPACIDADTANQUE, GALONESRESERVA, GALONESUTILIZABLES,
               GALONESTOLERANCIA, CHIPTANQUEO, NITPROPIETARIO, ACTIVO, ALIASPLACA
        FROM tblCabezotes
        WHERE ACTIVO = 1
        ORDER BY PLACA
        """;

    public async Task<IReadOnlyList<clsCabezoteLegadoDto>> ListarActivosAsync(CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirFpsapAsync(ct);
        var filas = await clsLegadoConexion.ConsultarAsync(cn, SQL, null, ct);
        return filas.Select(f => new clsCabezoteLegadoDto(
            f.Entero("ID"),
            f.Texto("PLACA") ?? string.Empty,
            f.Texto("TIPOMOTOR") ?? string.Empty,
            f.Decimal("GALONESCAPACIDADTANQUE"),
            f.Decimal("GALONESRESERVA"),
            f.Decimal("GALONESUTILIZABLES"),
            f.Decimal("GALONESTOLERANCIA"),
            f.Texto("CHIPTANQUEO"),
            f.Texto("NITPROPIETARIO"),
            f.Texto("ALIASPLACA"),
            f.Booleano("ACTIVO"))).ToList();
    }
}
