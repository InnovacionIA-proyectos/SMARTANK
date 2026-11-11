using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Common;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>
/// Upsert masivo de tblRutas por FO. OrigenLat/Lng y DestinoLat/Lng NO se escriben: los deriva el trigger de la tabla a partir
/// de la polilinea (primer y ultimo punto).
/// </summary>
public sealed class clsSqlRutasSincronizacionRepository(clsConexionSmartank conexion) : IRutasSincronizacionRepository
{
    private static clsEspecMasiva Espec()
    {
        string[] datos =
        [
            "ViajeId", "Fuente", "ValhallaId", "Kilometros", "NumPuntos", "NumPuntosOriginal", "Polilinea", "DetalleKm", "FechaCalculo",
        ];
        return new clsEspecMasiva(
            clsConexionSmartank.Tabla("tblRutas"),
            [
                new("Fo", typeof(string)), new("ViajeId", typeof(Guid)), new("Fuente", typeof(string)), new("ValhallaId", typeof(long)),
                new("Kilometros", typeof(decimal)), new("NumPuntos", typeof(int)), new("NumPuntosOriginal", typeof(int)),
                new("Polilinea", typeof(string)), new("DetalleKm", typeof(string)), new("FechaCalculo", typeof(DateTimeOffset)),
            ],
            ["Fo"],
            [.. clsAsignacionMasiva.Directas(datos), new clsAsignacionMasiva("SincronizadoAt", "SYSDATETIMEOFFSET()")],
            [.. clsAsignacionMasiva.Directas(["Fo", .. datos]), new clsAsignacionMasiva("SincronizadoAt", "SYSDATETIMEOFFSET()")]);
    }

    public async Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsRutaUpsertDto> rutas, CancellationToken ct = default)
    {
        var filas = rutas.Select(r => new object?[]
        {
            r.Fo, r.ViajeId, clsSnakeCase.ToSnake(r.Fuente), r.ValhallaId, clsValoresMasivos.Decimal(r.Kilometros, 3),
            r.NumPuntos, r.NumPuntosOriginal, r.PolilineaJson, r.DetalleKmJson, r.FechaCalculo,
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, Espec(), filas, f => $"FO {f[0]}", ct);
    }
}
