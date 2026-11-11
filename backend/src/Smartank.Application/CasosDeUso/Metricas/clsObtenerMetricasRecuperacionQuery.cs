using System.Globalization;
using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Application.CasosDeUso.Metricas.Interfaces;
using Smartank.Application.CasosDeUso.Metricas.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Domain.Metricas.Reglas;

namespace Smartank.Application.CasosDeUso.Metricas;

/// <summary>
/// KPIs de recuperacion de margen del tablero de Finanzas sobre el historial real de tanqueos. Los filtros llegan como
/// texto crudo del query string; como en el origen, uno que no se reconoce (anio no numerico) se ignora.
/// </summary>
public sealed record clsObtenerMetricasRecuperacionQuery(
    string? Programa,
    string? Proveedor,
    string? Producto,
    string? Anio,
    string? Mes,
    string? Ruta) : IQuery<clsMetricasRecuperacionDto>;

public sealed class clsObtenerMetricasRecuperacionQueryHandler(IMetricasRepository metricas)
    : IQueryHandler<clsObtenerMetricasRecuperacionQuery, clsMetricasRecuperacionDto>
{
    public async Task<clsMetricasRecuperacionDto> Handle(clsObtenerMetricasRecuperacionQuery query, CancellationToken ct = default)
    {
        var filtros = Normalizar(query);

        var tanqueos = await metricas.ListarTanqueosAsync(filtros, ct);
        var resultado = clsCalculoRecuperacion.Calcular(tanqueos.ToList());
        var opciones = await metricas.ObtenerOpcionesAsync(ct);

        return clsMetricasMapper.ADto(filtros, resultado, opciones);
    }

    public static clsRecuperacionFiltrosDto Normalizar(clsObtenerMetricasRecuperacionQuery q) => new(
        NullSiVacio(q.Programa),
        NullSiVacio(q.Proveedor),
        NullSiVacio(q.Producto),
        ParsearEntero(q.Anio),
        ParsearEntero(q.Mes),
        NullSiVacio(q.Ruta));

    private static string? NullSiVacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    /// <summary>Igual que el origen: cualquier numero finito se trunca a entero ("2026.9" -> 2026); texto no numerico -> sin filtro.</summary>
    private static int? ParsearEntero(string? texto)
    {
        if (!double.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) || !double.IsFinite(n))
            return null;
        var truncado = Math.Truncate(n);
        return truncado is >= int.MinValue and <= int.MaxValue ? (int)truncado : null;
    }
}
