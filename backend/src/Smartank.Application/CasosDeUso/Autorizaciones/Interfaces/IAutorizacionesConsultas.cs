using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

/// <summary>Filtros del listado de autorizaciones (todos opcionales).</summary>
public sealed record clsFiltroAutorizaciones(EstadoAutorizacion? Estado, Guid? ViajeId, int Limite);

/// <summary>Lecturas con joins (estacion, vehiculo, viaje, conductor, ruta) para las pantallas. Sin seguimiento ni efectos.</summary>
public interface IAutorizacionesConsultas
{
    /// <summary>Detalle de una autorizacion o null si no existe. <paramref name="rendimientoDefaultKmGal"/> completa la autonomia de vehiculos sin rendimiento.</summary>
    Task<clsAutorizacionDetalleDto?> ObtenerDetalleAsync(Guid id, double rendimientoDefaultKmGal, CancellationToken ct);

    /// <summary>Detalles, de la mas reciente a la mas antigua, con el filtro indicado.</summary>
    Task<IReadOnlyList<clsAutorizacionDetalleDto>> ListarDetallesAsync(
        clsFiltroAutorizaciones filtro, double rendimientoDefaultKmGal, CancellationToken ct);
}
