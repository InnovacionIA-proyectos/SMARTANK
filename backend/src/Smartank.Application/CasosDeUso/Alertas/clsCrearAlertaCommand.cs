using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Application.CasosDeUso.Alertas.Interfaces;
using Smartank.Application.CasosDeUso.Alertas.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Alertas;

/// <summary>Crea una alerta en estado Nueva. La invoca un servicio (secreto de servicio), no una persona: no hay usuario actor.</summary>
public sealed record clsCrearAlertaCommand(string? Tipo, Guid? VehiculoId, Guid? EventoId, double? MagnitudDesviacion)
    : ICommand<clsAlertaDto>;

public sealed class clsCrearAlertaCommandHandler(IAlertasRepository alertas, IUnitOfWork uow, IAuditoriaPort auditoria)
    : ICommandHandler<clsCrearAlertaCommand, clsAlertaDto>
{
    /// <summary>La columna MagnitudDesviacion es decimal(14,4): un tope evita desbordarla con un dato corrupto.</summary>
    public const double MAGNITUD_MAXIMA = 1_000_000_000;

    public async Task<clsAlertaDto> Handle(clsCrearAlertaCommand command, CancellationToken ct = default)
    {
        if (!clsSnakeCase.TryParse<TipoAlerta>(command.Tipo?.Trim(), out var tipo))
            throw new clsValidationFailedException(
                "TIPO_ALERTA_INVALIDO",
                $"'{command.Tipo}' no es un tipo de alerta válido. Use: desviacion_consumo_carga, desviacion_consumo, estacion_cara o ejecucion_parcial.");

        if (command.VehiculoId is not { } vehiculoId || vehiculoId == Guid.Empty)
            throw new clsValidationFailedException("VEHICULO_REQUERIDO", "vehiculo_id es requerido.");

        if (command.MagnitudDesviacion is { } m && (!double.IsFinite(m) || Math.Abs(m) > MAGNITUD_MAXIMA))
            throw new clsValidationFailedException("MAGNITUD_INVALIDA", "magnitud_desviacion está fuera de rango.");

        if (!await alertas.ExisteVehiculoAsync(vehiculoId, ct))
            throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "Vehículo no encontrado.");

        if (command.EventoId is { } eventoId && !await alertas.ExisteEventoAsync(eventoId, ct))
            throw new clsNotFoundException("EVENTO_NO_ENCONTRADO", "Evento de tanqueo no encontrado.");

        var alerta = new clsAlerta
        {
            Tipo = tipo,
            VehiculoId = vehiculoId,
            EventoId = command.EventoId,
            MagnitudDesviacion = command.MagnitudDesviacion,
            Estado = EstadoAlerta.Nueva,
        };
        alertas.Agregar(alerta);
        await uow.SaveChangesAsync(ct);

        await auditoria.RegistrarAsync(
            null, "crear_alerta", "tblAlertas", alerta.Id,
            new { tipo = clsSnakeCase.ToSnake(tipo), vehiculo_id = vehiculoId, magnitud_desviacion = command.MagnitudDesviacion }, ct);

        return clsAlertaMapper.ADto(alerta);
    }
}
