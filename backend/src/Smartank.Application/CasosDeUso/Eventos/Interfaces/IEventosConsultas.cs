using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Eventos.Interfaces;

/// <summary>Filtros del historial de tanqueos (todos opcionales). Las fechas son dias inclusivos.</summary>
public sealed record clsFiltroHistorial(string? Placa, FuenteTanqueo? Proveedor, DateOnly? Desde, DateOnly? Hasta);

/// <summary>
/// Fila del historial combinado: un tanqueo real (Terpel/Distracom) con la autorizacion de la app y su bitacora cuando se
/// pudieron casar (mismo vehiculo, fecha dentro de la ventana mas o menos 12 h y misma estacion por nombre).
/// </summary>
public sealed record clsHistorialTanqueoFila(
    Guid Id,
    FuenteTanqueo Proveedor,
    string Placa,
    string? TipoMotor,
    string? VehiculoPrograma,
    string? EstacionNombre,
    string? Ciudad,
    DateTimeOffset OcurridoAt,
    decimal? GalonesCargados,
    decimal? PrecioPagado,
    double? OdometroEvento,
    bool Autorizado,
    decimal? PrecioTecho,
    string? FotoSurtidorUrl,
    decimal? NivelAntes,
    decimal? NivelDespues);

public interface IEventosConsultas
{
    /// <summary>Historial combinado, del mas reciente al mas antiguo, con un tope de 2000 filas.</summary>
    Task<IReadOnlyList<clsHistorialTanqueoFila>> ListarHistorialAsync(clsFiltroHistorial filtro, CancellationToken ct);
}
