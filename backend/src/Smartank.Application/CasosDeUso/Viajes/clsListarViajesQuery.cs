using System.Globalization;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Viajes.DTOs;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Application.CasosDeUso.Viajes.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Viajes.Reglas;

namespace Smartank.Application.CasosDeUso.Viajes;

/// <summary>
/// Lista los viajes no cerrados con datos del vehiculo y de la ruta. Todos los parametros llegan como texto crudo del
/// query string: como en el origen, un valor que no se reconoce se ignora (no es un error), y esa normalizacion vive aqui.
/// </summary>
public sealed record clsListarViajesQuery(
    string? Estado,
    string? Ruta,
    string? Placa,
    string? Manifiesto,
    string? Anio,
    string? Mes,
    string? Desplazamiento,
    string? ConRecomendacion,
    string? Telemetria) : IQuery<clsViajesResponse>;

public sealed class clsListarViajesQueryHandler(
    IViajesLecturaRepository viajes,
    IOptions<clsParametrosNegocio> parametros,
    TimeProvider reloj)
    : IQueryHandler<clsListarViajesQuery, clsViajesResponse>
{
    /// <summary>Una lectura de nivel cuenta como "reciente" si tiene menos de esta antiguedad.</summary>
    public static readonly TimeSpan VENTANA_TELEMETRIA_RECIENTE = TimeSpan.FromHours(1);

    public async Task<clsViajesResponse> Handle(clsListarViajesQuery query, CancellationToken ct = default)
    {
        var filtro = Normalizar(query, reloj.GetUtcNow());

        var filas = await viajes.ListarAsync(filtro, ct);
        var opciones = await viajes.ObtenerOpcionesAsync(ct);

        var rendimiento = parametros.Value.RendimientoDefaultKmGal;
        var dtos = filas.Select(f => clsViajeMapper.ADto(f, rendimiento)).ToList();
        return new clsViajesResponse(dtos.Count, dtos, opciones);
    }

    /// <summary>Convierte los parametros crudos en un filtro tipado, descartando los valores no reconocidos.</summary>
    public static clsViajesFiltro Normalizar(clsListarViajesQuery q, DateTimeOffset ahora) => new(
        Estado: ParsearEstado(q.Estado),
        Ruta: NullSiVacio(q.Ruta),
        Placa: NullSiVacio(q.Placa),
        Manifiesto: NullSiVacio(q.Manifiesto),
        Anio: ParsearEntero(q.Anio),
        Mes: ParsearEntero(q.Mes),
        Desplazamiento: q.Desplazamiento switch
        {
            "vacio" => TipoDesplazamiento.Vacio,
            "cargado" => TipoDesplazamiento.Cargado,
            _ => null,
        },
        SoloRecomendacionActiva: q.ConRecomendacion == "1",
        TelemetriaReciente: q.Telemetria switch { "si" => true, "no" => false, _ => null },
        UmbralTelemetria: ahora - VENTANA_TELEMETRIA_RECIENTE);

    // Los cerrados no se listan nunca: el filtro por estado solo admite los otros tres.
    private static EstadoViaje? ParsearEstado(string? texto) =>
        clsSnakeCase.TryParse<EstadoViaje>(texto, out var estado) && estado != EstadoViaje.Cerrado ? estado : null;

    private static string? NullSiVacio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

    private static int? ParsearEntero(string? texto) =>
        int.TryParse(texto, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : null;
}
