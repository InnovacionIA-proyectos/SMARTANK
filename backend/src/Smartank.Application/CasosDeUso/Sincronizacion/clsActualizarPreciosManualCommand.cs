using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Carga manual de precios por galon de EDS (respaldo cuando las fuentes automaticas no estan disponibles). Valida todo
/// antes de escribir: o se actualizan todas las estaciones indicadas o ninguna.
/// </summary>
public sealed record clsActualizarPreciosManualCommand(Guid? ActorId, IReadOnlyList<clsPrecioManualItemDto> Precios)
    : ICommand<clsResultadoPreciosManualDto>;

public sealed class clsActualizarPreciosManualCommandHandler(
    IEstacionesSincronizacionRepository estaciones,
    IAuditoriaPort auditoria)
    : ICommandHandler<clsActualizarPreciosManualCommand, clsResultadoPreciosManualDto>
{
    public const string MODO_MANUAL = "manual";

    public async Task<clsResultadoPreciosManualDto> Handle(clsActualizarPreciosManualCommand command, CancellationToken ct = default)
    {
        if (command.Precios.Count == 0)
            throw new clsValidationFailedException("PRECIOS_VACIOS", "Debe indicar al menos un precio.");
        if (command.Precios.Any(p => p.PrecioGalonActual < 0))
            throw new clsValidationFailedException("PRECIO_INVALIDO", "El precio por galon no puede ser negativo.");

        var faltantes = await estaciones.ObtenerInexistentesAsync(command.Precios.Select(p => p.EstacionId).Distinct().ToList(), ct);
        if (faltantes.Count > 0)
            throw new clsNotFoundException("ESTACION_NO_ENCONTRADA", $"Estacion {faltantes[0]} no encontrada.");

        var actualizadas = new List<clsEstacionPrecioDto>(command.Precios.Count);
        foreach (var p in command.Precios)
        {
            var estacion = await estaciones.ActualizarPrecioGalonAsync(p.EstacionId, p.PrecioGalonActual, ct)
                ?? throw new clsNotFoundException("ESTACION_NO_ENCONTRADA", $"Estacion {p.EstacionId} no encontrada.");
            actualizadas.Add(estacion);
        }

        await auditoria.RegistrarAsync(
            command.ActorId, "sync_precios_manual", "estaciones", null, new { actualizadas = actualizadas.Count }, ct);

        return new clsResultadoPreciosManualDto(MODO_MANUAL, actualizadas.Count, actualizadas);
    }
}
