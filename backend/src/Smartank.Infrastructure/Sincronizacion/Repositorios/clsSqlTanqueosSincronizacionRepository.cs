using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Common;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>Upsert masivo de tblTanqueos por (Fuente, FuenteId).</summary>
public sealed class clsSqlTanqueosSincronizacionRepository(clsConexionSmartank conexion) : ITanqueosSincronizacionRepository
{
    private static clsEspecMasiva Espec()
    {
        string[] datos =
        [
            "VehiculoId", "Placa", "Fecha", "Galones", "ValorGalon", "ValorTotal", "Producto", "FamiliaProducto", "Ciudad",
            "Estacion", "Kilometraje", "CedulaConductor", "Factura", "Referencia", "CentroCosto",
        ];
        return new clsEspecMasiva(
            clsConexionSmartank.Tabla("tblTanqueos"),
            [
                new("Fuente", typeof(string)), new("FuenteId", typeof(long)), new("VehiculoId", typeof(Guid)), new("Placa", typeof(string)),
                new("Fecha", typeof(DateTimeOffset)), new("Galones", typeof(decimal)), new("ValorGalon", typeof(decimal)),
                new("ValorTotal", typeof(decimal)), new("Producto", typeof(string)), new("FamiliaProducto", typeof(string)),
                new("Ciudad", typeof(string)), new("Estacion", typeof(string)), new("Kilometraje", typeof(decimal)),
                new("CedulaConductor", typeof(string)), new("Factura", typeof(string)), new("Referencia", typeof(string)),
                new("CentroCosto", typeof(string)),
            ],
            ["Fuente", "FuenteId"],
            [.. clsAsignacionMasiva.Directas(datos), new clsAsignacionMasiva("SincronizadoAt", "SYSDATETIMEOFFSET()")],
            [.. clsAsignacionMasiva.Directas(["Fuente", "FuenteId", .. datos]), new clsAsignacionMasiva("SincronizadoAt", "SYSDATETIMEOFFSET()")]);
    }

    public async Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsTanqueoUpsertDto> tanqueos, CancellationToken ct = default)
    {
        var filas = tanqueos.Select(t =>
        {
            var o = t.Origen;
            return new object?[]
            {
                clsSnakeCase.ToSnake(o.Fuente), o.FuenteId, t.VehiculoId, o.Placa, o.Fecha,
                clsValoresMasivos.Decimal(o.Galones, 3), clsValoresMasivos.Decimal(o.ValorGalon, 2), clsValoresMasivos.Decimal(o.ValorTotal, 2),
                clsValoresMasivos.Texto(o.Producto), clsValoresMasivos.Texto(o.FamiliaProducto), clsValoresMasivos.Texto(o.Ciudad),
                clsValoresMasivos.Texto(o.Estacion), clsValoresMasivos.Decimal(o.Kilometraje, 3), clsValoresMasivos.Texto(o.CedulaConductor),
                clsValoresMasivos.Texto(o.Factura), clsValoresMasivos.Texto(o.Referencia), clsValoresMasivos.Texto(o.CentroCosto),
            };
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, Espec(), filas, f => $"{f[0]} {f[1]} ({f[3]})", ct);
    }
}
