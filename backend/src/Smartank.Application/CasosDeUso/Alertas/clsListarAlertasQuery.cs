using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Alertas;

/// <summary>Bandeja de alertas de Control de Flota, opcionalmente filtrada por estado (texto snake_case, p. ej. "en_revision").</summary>
public sealed record clsListarAlertasQuery(string? Estado) : IQuery<clsAlertasResponse>;

public sealed class clsListarAlertasQueryHandler(IAlertasRepository alertas)
    : IQueryHandler<clsListarAlertasQuery, clsAlertasResponse>
{
    public async Task<clsAlertasResponse> Handle(clsListarAlertasQuery query, CancellationToken ct = default)
    {
        var estado = ParsearEstadoOpcional(query.Estado);
        var lista = await alertas.ListarAsync(estado, ct);
        return new clsAlertasResponse(estado, lista.Count, lista);
    }

    /// <summary>Vacio = sin filtro; un texto que no es un estado de alerta es un error de validacion (400), no un filtro ignorado.</summary>
    internal static EstadoAlerta? ParsearEstadoOpcional(string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return null;
        return clsSnakeCase.TryParse<EstadoAlerta>(texto.Trim(), out var estado)
            ? estado
            : throw EstadoInvalido(texto);
    }

    internal static clsValidationFailedException EstadoInvalido(string? texto) =>
        new("ESTADO_ALERTA_INVALIDO",
            $"'{texto}' no es un estado de alerta válido. Use: nueva, en_revision, gestionada o descartada.");
}
