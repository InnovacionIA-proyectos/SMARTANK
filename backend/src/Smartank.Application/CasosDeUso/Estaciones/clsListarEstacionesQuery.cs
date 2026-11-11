using Smartank.Application.CasosDeUso.Estaciones.DTOs;
using Smartank.Application.CasosDeUso.Estaciones.Interfaces;
using Smartank.Application.CasosDeUso.Estaciones.Mappers;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Estaciones;

/// <summary>Lista las estaciones del catalogo, opcionalmente de una sola ruta.</summary>
public sealed record clsListarEstacionesQuery(string? Ruta) : IQuery<clsEstacionesResponse>;

public sealed class clsListarEstacionesQueryHandler(IEstacionesRepository estaciones)
    : IQueryHandler<clsListarEstacionesQuery, clsEstacionesResponse>
{
    public async Task<clsEstacionesResponse> Handle(clsListarEstacionesQuery query, CancellationToken ct = default)
    {
        // Un filtro en blanco (?ruta=) equivale a no filtrar.
        var ruta = string.IsNullOrWhiteSpace(query.Ruta) ? null : query.Ruta.Trim();

        var lista = await estaciones.ListarAsync(ruta, ct);
        return new clsEstacionesResponse(ruta, lista.Count, lista.Select(clsEstacionMapper.ADto).ToList());
    }
}
