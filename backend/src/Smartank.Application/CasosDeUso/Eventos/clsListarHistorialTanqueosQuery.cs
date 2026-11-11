using System.Globalization;
using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Eventos;

/// <summary>
/// Historial de tanqueos COMBINADO: los tanqueos reales (Terpel/Distracom) con la autorizacion y la bitacora de la app cuando se
/// pueden casar. Filtros opcionales: placa (parcial), proveedor (terpel | distracom) y rango de fechas (yyyy-MM-dd, inclusivo).
/// </summary>
public sealed record clsListarHistorialTanqueosQuery(string? Placa, string? Proveedor, string? Desde, string? Hasta)
    : IQuery<clsHistorialTanqueosDto>;

public sealed class clsListarHistorialTanqueosQueryHandler(IEventosConsultas consultas)
    : IQueryHandler<clsListarHistorialTanqueosQuery, clsHistorialTanqueosDto>
{
    /// <summary>Texto cuando una fila del historial no trae ni estacion ni ciudad.</summary>
    private const string SIN_ESTACION = "—";

    public async Task<clsHistorialTanqueosDto> Handle(clsListarHistorialTanqueosQuery query, CancellationToken ct = default)
    {
        var filtro = new clsFiltroHistorial(
            clsTexto.NullifyBlank(query.Placa),
            ParsearProveedor(query.Proveedor),
            ParsearFecha(query.Desde, "desde"),
            ParsearFecha(query.Hasta, "hasta"));

        var filas = await consultas.ListarHistorialAsync(filtro, ct);
        var eventos = filas.Select(f => new clsHistorialTanqueoItemDto(
            f.Id,
            f.OcurridoAt,
            f.Placa,
            f.TipoMotor ?? string.Empty,
            f.VehiculoPrograma,
            clsTexto.NullifyBlank(f.EstacionNombre) ?? clsTexto.NullifyBlank(f.Ciudad) ?? SIN_ESTACION,
            f.Ciudad, // el historial real no trae ruta: se usa la ciudad
            f.Proveedor,
            f.GalonesCargados ?? 0,
            f.PrecioPagado,
            f.OdometroEvento,
            f.Autorizado,
            f.PrecioTecho,
            f.FotoSurtidorUrl,
            f.NivelAntes,
            f.NivelDespues)).ToList();
        return new clsHistorialTanqueosDto(eventos.Count, eventos);
    }

    private static FuenteTanqueo? ParsearProveedor(string? proveedor)
    {
        if (string.IsNullOrWhiteSpace(proveedor)) return null;
        return clsSnakeCase.TryParse<FuenteTanqueo>(proveedor.Trim(), out var fuente)
            ? fuente
            : throw new clsValidationFailedException("PROVEEDOR_INVALIDO", "El proveedor debe ser terpel o distracom.");
    }

    private static DateOnly? ParsearFecha(string? fecha, string nombre)
    {
        if (string.IsNullOrWhiteSpace(fecha)) return null;
        return DateOnly.TryParseExact(fecha.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : throw new clsValidationFailedException("FECHA_INVALIDA", $"La fecha '{nombre}' es inválida (formato yyyy-MM-dd).");
    }
}
