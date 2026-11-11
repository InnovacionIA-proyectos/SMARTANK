using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Application.CasosDeUso.Eventos.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Eventos.Reglas;

namespace Smartank.Application.CasosDeUso.Eventos;

/// <summary>
/// Verifica una carga: cruza el evento de bitacora contra su autorizacion y la telemetria, crea las alertas que correspondan
/// (desviacion de consumo de carga, ejecucion parcial, estacion cara) y fija el estado final de la autorizacion
/// (Ejecutada o EjecutadaParcial). Solo aplica a una autorizacion Autorizada: una segunda verificacion se rechaza para no
/// duplicar alertas.
/// </summary>
public sealed record clsVerificarEventoCommand(Guid EventoId) : ICommand<clsVerificacionDto>;

public sealed class clsVerificarEventoCommandHandler(
    IEventosRepository eventos,
    IAutorizacionesRepository autorizaciones,
    IContextoTanqueoRepository contexto,
    IAuditoriaPort auditoria,
    IUnitOfWork uow,
    ICurrentUserPort usuarioActual,
    IOptions<clsParametrosNegocio> parametros)
    : ICommandHandler<clsVerificarEventoCommand, clsVerificacionDto>
{
    public async Task<clsVerificacionDto> Handle(clsVerificarEventoCommand command, CancellationToken ct = default)
    {
        var evento = await eventos.ObtenerAsync(command.EventoId, ct)
            ?? throw new clsNotFoundException("EVENTO_NO_ENCONTRADO", "Evento de tanqueo no encontrado.");
        var autorizacion = await autorizaciones.ObtenerAsync(evento.AutorizacionId, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "Autorización del evento no encontrada.");
        var estacion = await contexto.ObtenerEstacionAsync(evento.EstacionId, ct)
            ?? throw new clsNotFoundException("ESTACION_NO_ENCONTRADA", "Estación del evento no encontrada.");

        var de = autorizacion.Estado;
        if (clsMaquinaEstadosAutorizacion.FueEjecutada(de))
            throw new clsConflictException("EVENTO_YA_VERIFICADO", "La carga de esta autorización ya fue verificada.");

        // La referencia de "estacion cara" es la EDS mas barata de la misma ruta; sin ruta o sin precio no hay referencia.
        decimal? referencia = estacion.Ruta is { Length: > 0 } ruta && estacion.PrecioGalonActual is not null
            ? await eventos.ObtenerPrecioMinimoDeRutaAsync(ruta, ct)
            : null;

        var p = parametros.Value;
        var resultado = clsVerificacionCarga.Evaluar(new clsDatosVerificacion(
            evento.GalonesCargados,
            evento.NivelAntes,
            evento.NivelDespues,
            autorizacion.GalonesMax,
            estacion.PrecioGalonActual,
            referencia,
            p.ToleranciaAutonomiaDefault,
            p.UmbralGalonFantasma));

        if (!clsMaquinaEstadosAutorizacion.PuedeTransicionar(de, resultado.EstadoFinal))
            throw new clsConflictException(
                "TRANSICION_INVALIDA",
                $"La autorización debe estar 'autorizada' para verificarla (estado actual: {clsSnakeCase.ToSnake(de)}).");

        var alertas = resultado.Alertas
            .Select(a => new clsAlerta
            {
                Tipo = a.Tipo,
                VehiculoId = evento.VehiculoId,
                EventoId = evento.Id,
                MagnitudDesviacion = a.Magnitud,
            })
            .ToList();
        foreach (var alerta in alertas) eventos.AgregarAlerta(alerta);
        autorizacion.Estado = resultado.EstadoFinal;

        // Alertas, estado final y auditoria se guardan juntos; el rowversion de la autorizacion evita verificar dos veces en paralelo.
        await auditoria.RegistrarAsync(
            usuarioActual.UsuarioId, "verificar_carga", "eventos_tanqueo", evento.Id,
            new
            {
                estado = new[] { clsSnakeCase.ToSnake(de), clsSnakeCase.ToSnake(resultado.EstadoFinal) },
                estado_autorizacion = clsSnakeCase.ToSnake(resultado.EstadoFinal),
                alertas = alertas.Select(a => clsSnakeCase.ToSnake(a.Tipo)).ToArray(),
            }, ct);
        await uow.SaveChangesAsync(ct);

        return new clsVerificacionDto(
            evento.Id, resultado.EstadoFinal, alertas.Count, alertas.Select(clsEventoMapper.ToDto).ToList());
    }
}
