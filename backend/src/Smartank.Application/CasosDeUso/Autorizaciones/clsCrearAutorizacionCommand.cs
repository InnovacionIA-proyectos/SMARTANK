using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Autorizaciones.Mappers;
using Smartank.Application.CasosDeUso.Autorizaciones.Validadores;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones;

/// <summary>Crea una autorizacion (propuesta) a mano con galones maximos, precio techo y ventana. Los campos van como opcionales para que la validacion responda con el contrato de error comun.</summary>
public sealed record clsCrearAutorizacionCommand(
    Guid? ViajeId,
    Guid? VehiculoId,
    Guid? EstacionId,
    decimal? GalonesMax,
    decimal? PrecioTecho,
    decimal? DeltaAhorroEstimado,
    DateTimeOffset? FechaInicio,
    DateTimeOffset? FechaFin) : ICommand<clsAutorizacionDto>;

public sealed class clsCrearAutorizacionCommandHandler(
    IAutorizacionesRepository autorizaciones,
    IContextoTanqueoRepository contexto,
    IAuditoriaPort auditoria,
    IUnitOfWork uow,
    ICurrentUserPort usuarioActual)
    : ICommandHandler<clsCrearAutorizacionCommand, clsAutorizacionDto>
{
    public async Task<clsAutorizacionDto> Handle(clsCrearAutorizacionCommand command, CancellationToken ct = default)
    {
        clsAutorizacionValidador.Validar(
            command.ViajeId, command.VehiculoId, command.EstacionId,
            command.GalonesMax, command.PrecioTecho, command.FechaInicio, command.FechaFin);

        var viaje = await contexto.ObtenerViajeAsync(command.ViajeId!.Value, ct)
            ?? throw new clsNotFoundException("VIAJE_NO_ENCONTRADO", "Viaje no encontrado.");
        if (viaje.VehiculoId != command.VehiculoId)
            throw new clsValidationFailedException("VEHICULO_NO_CORRESPONDE", "El vehículo no corresponde al viaje.");
        _ = await contexto.ObtenerEstacionAsync(command.EstacionId!.Value, ct)
            ?? throw new clsNotFoundException("ESTACION_NO_ENCONTRADA", "Estación no encontrada.");

        // La base solo admite una propuesta por viaje; se valida antes de insertar para responder con un codigo claro.
        if (await autorizaciones.ObtenerPropuestaDeViajeAsync(viaje.Id, ct) is not null)
            throw PropuestaExistente();

        var autorizacion = new clsAutorizacionTanqueo
        {
            ViajeId = viaje.Id,
            VehiculoId = viaje.VehiculoId,
            EstacionId = command.EstacionId.Value,
            GalonesMax = command.GalonesMax!.Value,
            PrecioTecho = command.PrecioTecho!.Value,
            DeltaAhorroEstimado = command.DeltaAhorroEstimado,
            FechaInicio = command.FechaInicio!.Value,
            FechaFin = command.FechaFin!.Value,
            Estado = EstadoAutorizacion.Propuesta,
        };
        autorizaciones.Agregar(autorizacion);

        try
        {
            // La auditoria guarda junto con la autorizacion: el alta y su rastro son atomicos.
            await auditoria.RegistrarAsync(
                usuarioActual.UsuarioId, "crear_autorizacion", "autorizaciones_tanqueo", autorizacion.Id,
                new { estado = "propuesta" }, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch (clsConflictException ex) when (ex.Code == "REGISTRO_DUPLICADO")
        {
            // Carrera: otro usuario creo la propuesta del viaje entre la validacion y el guardado.
            throw PropuestaExistente();
        }

        return clsAutorizacionMapper.ToDto(autorizacion);
    }

    private static clsConflictException PropuestaExistente() =>
        new("PROPUESTA_EXISTENTE", "El viaje ya tiene una propuesta de tanqueo; autorícela o recomiende de nuevo para actualizarla.");
}
