using Microsoft.Extensions.Logging;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Application.CasosDeUso.Eventos.Mappers;
using Smartank.Application.CasosDeUso.Eventos.Validadores;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Domain.Eventos.Reglas;

namespace Smartank.Application.CasosDeUso.Eventos;

/// <summary>
/// El conductor reporta su carga con el token de su enlace (sin sesion). La autorizacion debe estar Autorizada y dentro de su
/// ventana, y admite un solo evento. Tras guardarlo se verifica la carga de inmediato; si la verificacion falla, el evento
/// queda guardado y el error se registra en el log.
/// </summary>
public sealed record clsRegistrarEventoCommand(
    string? Token,
    decimal? GalonesCargados,
    decimal? PrecioPagado,
    string? FotoSurtidorUrl,
    double? OdometroEvento,
    decimal? NivelAntes,
    decimal? NivelDespues) : ICommand<clsEventoDto>;

public sealed class clsRegistrarEventoCommandHandler(
    IBitacoraTokenPort tokens,
    IAutorizacionesRepository autorizaciones,
    IContextoTanqueoRepository contexto,
    IEventosRepository eventos,
    ICommandHandler<clsVerificarEventoCommand, clsVerificacionDto> verificar,
    IAuditoriaPort auditoria,
    IUnitOfWork uow,
    TimeProvider reloj,
    ILogger<clsRegistrarEventoCommandHandler> logger)
    : ICommandHandler<clsRegistrarEventoCommand, clsEventoDto>
{
    public async Task<clsEventoDto> Handle(clsRegistrarEventoCommand command, CancellationToken ct = default)
    {
        // El token se valida primero: una peticion anonima sin credencial valida no debe enterarse de nada mas.
        var autorizacionId = tokens.Validar(command.Token ?? string.Empty);
        clsEventoValidador.Validar(
            command.GalonesCargados, command.PrecioPagado, command.FotoSurtidorUrl,
            command.OdometroEvento, command.NivelAntes, command.NivelDespues);

        var autorizacion = await autorizaciones.ObtenerAsync(autorizacionId, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "La autorización del enlace no existe.");

        // "Ya registrada" va antes que el estado: tras registrar, la verificacion deja la autorizacion Ejecutada, y quien
        // reintenta debe recibir este mensaje y no uno sobre el estado.
        if (await eventos.ExistePorAutorizacionAsync(autorizacion.Id, ct))
            throw YaRegistrada();
        if (autorizacion.Estado != EstadoAutorizacion.Autorizada)
            throw new clsConflictException(
                "AUTORIZACION_NO_AUTORIZADA",
                $"La autorización no está autorizada (estado actual: {clsSnakeCase.ToSnake(autorizacion.Estado)}).");

        var ahora = reloj.GetUtcNow();
        if (!clsReglasBitacora.EstaDentroDeVentana(autorizacion.FechaInicio, autorizacion.FechaFin, ahora))
            throw new clsConflictException(
                "AUTORIZACION_FUERA_DE_VENTANA", "La autorización no está vigente: la carga se reportó fuera de su ventana.");

        var vehiculo = await contexto.ObtenerVehiculoAsync(autorizacion.VehiculoId, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "Vehículo de la autorización no encontrado.");

        // Programa y producto son una instantanea del vehiculo al momento de la carga (el programa puede cambiar despues).
        var evento = new clsEventoTanqueo
        {
            AutorizacionId = autorizacion.Id,
            VehiculoId = autorizacion.VehiculoId,
            EstacionId = autorizacion.EstacionId,
            GalonesCargados = command.GalonesCargados!.Value,
            PrecioPagado = command.PrecioPagado,
            FotoSurtidorUrl = clsTexto.NullifyBlank(command.FotoSurtidorUrl),
            OdometroEvento = command.OdometroEvento,
            NivelAntes = command.NivelAntes,
            NivelDespues = command.NivelDespues,
            RegistradoPor = null,
            Programa = vehiculo.Programa,
            Producto = clsReglasBitacora.DerivarProducto(vehiculo.TipoMotor),
            OcurridoAt = ahora,
        };
        eventos.Agregar(evento);

        try
        {
            await auditoria.RegistrarAsync(
                null, "registrar_evento", "eventos_tanqueo", evento.Id,
                new { galones_cargados = evento.GalonesCargados }, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch (clsConflictException ex) when (ex.Code == "REGISTRO_DUPLICADO")
        {
            // Carrera: otro reporte de la misma autorizacion se guardo primero (indice unico en la base).
            throw YaRegistrada();
        }

        await VerificarSinFallarAsync(evento.Id, ct);
        return clsEventoMapper.ToDto(evento);
    }

    /// <summary>La verificacion es un efecto derivado: si falla, el reporte del conductor ya esta guardado y no se pierde.</summary>
    private async Task VerificarSinFallarAsync(Guid eventoId, CancellationToken ct)
    {
        try
        {
            await verificar.Handle(new clsVerificarEventoCommand(eventoId), ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "La verificación automática del evento {EventoId} falló; el evento quedó guardado.", eventoId);
        }
    }

    private static clsConflictException YaRegistrada() =>
        new("BITACORA_YA_REGISTRADA", "La bitácora de esta autorización ya fue registrada.");
}
