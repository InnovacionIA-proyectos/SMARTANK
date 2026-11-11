using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>Upsert masivo de tblConductores por cedula.</summary>
public sealed class clsSqlConductoresSincronizacionRepository(clsConexionSmartank conexion) : IConductoresSincronizacionRepository
{
    private static clsEspecMasiva Espec()
    {
        string[] datos = ["Nombres", "NumeroCelular", "Activo", "UtilizarTarjetaPrepago", "FpsapId"];
        return new clsEspecMasiva(
            clsConexionSmartank.Tabla("tblConductores"),
            [
                new("Cedula", typeof(string)), new("Nombres", typeof(string)), new("NumeroCelular", typeof(string)),
                new("Activo", typeof(bool)), new("UtilizarTarjetaPrepago", typeof(bool)), new("FpsapId", typeof(long)),
            ],
            ["Cedula"],
            [.. clsAsignacionMasiva.Directas(datos), new clsAsignacionMasiva("FpsapSincronizadoAt", "SYSDATETIMEOFFSET()")],
            [.. clsAsignacionMasiva.Directas(["Cedula", .. datos]), new clsAsignacionMasiva("FpsapSincronizadoAt", "SYSDATETIMEOFFSET()")]);
    }

    public async Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsConductorUpsertDto> conductores, CancellationToken ct = default)
    {
        var filas = conductores.Select(c => new object?[]
        {
            c.Cedula, c.Nombres, clsValoresMasivos.Texto(c.NumeroCelular), c.Activo, c.UtilizarTarjetaPrepago, c.FpsapId,
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, Espec(), filas, f => (string)f[0]!, ct);
    }
}
