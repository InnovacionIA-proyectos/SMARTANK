using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones;

/// <summary>Lista autorizaciones (mas recientes primero), opcionalmente por estado (texto snake_case) y por viaje.</summary>
public sealed record clsListarAutorizacionesQuery(string? Estado, Guid? ViajeId) : IQuery<clsAutorizacionesListaDto>;

public sealed class clsListarAutorizacionesQueryHandler(
    IAutorizacionesConsultas consultas,
    IOptions<clsParametrosNegocio> parametros)
    : IQueryHandler<clsListarAutorizacionesQuery, clsAutorizacionesListaDto>
{
    /// <summary>Tope de filas por respuesta: el listado no pagina.</summary>
    public const int LIMITE = 200;

    public async Task<clsAutorizacionesListaDto> Handle(clsListarAutorizacionesQuery query, CancellationToken ct = default)
    {
        EstadoAutorizacion? estado = null;
        if (!string.IsNullOrWhiteSpace(query.Estado))
        {
            if (!clsSnakeCase.TryParse<EstadoAutorizacion>(query.Estado.Trim(), out var parseado))
                throw new clsValidationFailedException(
                    "ESTADO_INVALIDO",
                    "El estado debe ser uno de: propuesta, autorizada, ejecutada, ejecutada_parcial, expirada, rechazada.");
            estado = parseado;
        }

        var detalles = await consultas.ListarDetallesAsync(
            new clsFiltroAutorizaciones(estado, query.ViajeId, LIMITE), parametros.Value.RendimientoDefaultKmGal, ct);
        return new clsAutorizacionesListaDto(detalles.Count, detalles);
    }
}
