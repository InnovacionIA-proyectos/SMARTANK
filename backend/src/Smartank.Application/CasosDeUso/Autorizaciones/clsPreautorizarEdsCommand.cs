using System.Text.Json;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Autorizaciones.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones;

/// <summary>Envia a la EDS la pre-autorizacion (placa, galones, ventana) de una autorizacion ya autorizada y guarda su confirmacion.</summary>
public sealed record clsPreautorizarEdsCommand(Guid AutorizacionId) : ICommand<clsPreautorizacionEdsDto>;

public sealed class clsPreautorizarEdsCommandHandler(
    IAutorizacionesRepository autorizaciones,
    IContextoTanqueoRepository contexto,
    IEdsPort eds,
    IAuditoriaPort auditoria,
    IUnitOfWork uow,
    ICurrentUserPort usuarioActual)
    : ICommandHandler<clsPreautorizarEdsCommand, clsPreautorizacionEdsDto>
{
    private static readonly JsonSerializerOptions JSON = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    public async Task<clsPreautorizacionEdsDto> Handle(clsPreautorizarEdsCommand command, CancellationToken ct = default)
    {
        var autorizacion = await autorizaciones.ObtenerAsync(command.AutorizacionId, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "Autorización no encontrada.");
        if (autorizacion.Estado != EstadoAutorizacion.Autorizada)
            throw new clsConflictException(
                "TRANSICION_INVALIDA",
                $"La autorización debe estar 'autorizada' (estado actual: {clsSnakeCase.ToSnake(autorizacion.Estado)}).");

        var estacion = await contexto.ObtenerEstacionAsync(autorizacion.EstacionId, ct)
            ?? throw new clsNotFoundException("ESTACION_NO_ENCONTRADA", "Estación de la autorización no encontrada.");
        if (!estacion.SoportaPreautorizacion)
            throw new clsConflictException(
                "ESTACION_SIN_PREAUTORIZACION", "La estación no soporta pre-autorización en el surtidor.");

        var vehiculo = await contexto.ObtenerVehiculoAsync(autorizacion.VehiculoId, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "Vehículo de la autorización no encontrado.");

        var confirmacion = await eds.RegistrarPreautorizacionAsync(
            new clsPreautorizacionEdsSolicitud(vehiculo.Placa, autorizacion.GalonesMax, autorizacion.FechaInicio, autorizacion.FechaFin), ct);

        autorizacion.EdsConfirmacion = JsonSerializer.Serialize(confirmacion, JSON);
        await auditoria.RegistrarAsync(
            usuarioActual.UsuarioId, "preautorizar_eds", "autorizaciones_tanqueo", autorizacion.Id,
            new { referencia = confirmacion.Referencia }, ct);
        await uow.SaveChangesAsync(ct);

        return new clsPreautorizacionEdsDto(clsAutorizacionMapper.ToDto(autorizacion), confirmacion);
    }
}
