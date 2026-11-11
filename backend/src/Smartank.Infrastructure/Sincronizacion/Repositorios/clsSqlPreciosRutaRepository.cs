using System.Runtime.CompilerServices;
using System.Text.Json;
using Dapper;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Geo;

namespace Smartank.Infrastructure.Sincronizacion.Repositorios;

/// <summary>Lee las polilineas de tblRutas en flujo y escribe el precio de referencia por viaje en tblViajePreciosRef.</summary>
public sealed class clsSqlPreciosRutaRepository(clsConexionSmartank conexion) : IPreciosRutaRepository
{
    private sealed record clsFilaRuta(Guid ViajeId, string Polilinea);

    private static clsEspecMasiva Espec() => new(
        clsConexionSmartank.Tabla("tblViajePreciosRef"),
        [new("ViajeId", typeof(Guid)), new("AcpmMin", typeof(decimal)), new("AcpmMax", typeof(decimal)), new("EdsCerca", typeof(int))],
        ["ViajeId"],
        [.. clsAsignacionMasiva.Directas("AcpmMin", "AcpmMax", "EdsCerca"), new clsAsignacionMasiva("CalculadoAt", "SYSDATETIMEOFFSET()")],
        [.. clsAsignacionMasiva.Directas("ViajeId", "AcpmMin", "AcpmMax", "EdsCerca"), new clsAsignacionMasiva("CalculadoAt", "SYSDATETIMEOFFSET()")]);

    public async IAsyncEnumerable<clsRutaParaPrecioDto> ListarRutasConPolilineaAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var cn = await conexion.AbrirAsync(ct);

        // Con al menos 2 puntos: el segundo existe si JSON_VALUE('$[1][0]') no es NULL.
        var consulta = $"""
            SELECT ViajeId, Polilinea
            FROM {clsConexionSmartank.Tabla("tblRutas")}
            WHERE ViajeId IS NOT NULL AND Polilinea IS NOT NULL AND JSON_VALUE(Polilinea, '$[1][0]') IS NOT NULL
            """;

        await foreach (var fila in cn.QueryUnbufferedAsync<clsFilaRuta>(consulta, commandTimeout: 300).WithCancellation(ct))
        {
            var puntos = Parsear(fila.Polilinea);
            if (puntos.Count >= 2) yield return new clsRutaParaPrecioDto(fila.ViajeId, puntos);
        }
    }

    public async Task<clsResultadoUpsertDto> UpsertAsync(IReadOnlyList<clsPrecioReferenciaViajeDto> precios, CancellationToken ct = default)
    {
        var filas = precios.Select(p => new object?[]
        {
            p.ViajeId, clsValoresMasivos.Decimal(p.AcpmMin, 2), clsValoresMasivos.Decimal(p.AcpmMax, 2), p.EdsCerca,
        }).ToList();

        await using var cn = await conexion.AbrirAsync(ct);
        return await clsUpsertMasivo.EjecutarAsync(cn, Espec(), filas, f => $"viaje {f[0]}", ct);
    }

    /// <summary>Lee <c>[[lat,lng],...]</c>; un JSON danado o con forma inesperada deja la ruta fuera (lista vacia) en vez de romper la corrida.</summary>
    private static List<clsCoordenada> Parsear(string json)
    {
        try
        {
            var pares = JsonSerializer.Deserialize<double[][]>(json);
            if (pares is null) return [];
            return pares.Where(p => p is { Length: >= 2 }).Select(p => new clsCoordenada(p[0], p[1])).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
