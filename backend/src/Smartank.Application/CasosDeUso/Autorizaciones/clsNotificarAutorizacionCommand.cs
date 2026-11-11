using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Excepciones;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Autorizaciones;

/// <summary>
/// Envia la orden de tanqueo al conductor por WhatsApp con la estacion, el mapa, los galones y el enlace de su bitacora.
/// Exige una autorizacion Autorizada. Si WhatsApp no esta configurado o el envio falla, no falla la peticion: responde
/// <c>whatsapp.enviado = false</c> con el motivo.
/// </summary>
public sealed record clsNotificarAutorizacionCommand(Guid AutorizacionId) : ICommand<clsNotificarDto>;

public sealed class clsNotificarAutorizacionCommandHandler(
    IAutorizacionesRepository autorizaciones,
    IContextoTanqueoRepository contexto,
    IBitacoraTokenPort tokens,
    IWhatsappPort whatsapp,
    IAuditoriaPort auditoria,
    ICurrentUserPort usuarioActual,
    IOptions<clsAplicacionOptions> aplicacion)
    : ICommandHandler<clsNotificarAutorizacionCommand, clsNotificarDto>
{
    private const string NOMBRE_DESTINATARIO_GENERICO = "Conductor";

    public async Task<clsNotificarDto> Handle(clsNotificarAutorizacionCommand command, CancellationToken ct = default)
    {
        var autorizacion = await autorizaciones.ObtenerAsync(command.AutorizacionId, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "Autorización no encontrada.");
        if (autorizacion.Estado != EstadoAutorizacion.Autorizada)
            throw new clsConflictException(
                "TRANSICION_INVALIDA",
                $"La autorización debe estar 'autorizada' (estado actual: {clsSnakeCase.ToSnake(autorizacion.Estado)}).");

        var estacion = await contexto.ObtenerEstacionAsync(autorizacion.EstacionId, ct)
            ?? throw new clsNotFoundException("ESTACION_NO_ENCONTRADA", "Estación de la autorización no encontrada.");
        var vehiculo = await contexto.ObtenerVehiculoAsync(autorizacion.VehiculoId, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "Vehículo de la autorización no encontrado.");
        var viaje = await contexto.ObtenerViajeAsync(autorizacion.ViajeId, ct)
            ?? throw new clsNotFoundException("VIAJE_NO_ENCONTRADO", "Viaje de la autorización no encontrado.");

        var destinatario = await ResolverDestinatarioAsync(viaje.CedulaConductor, ct);

        // El enlace lleva un token firmado (no el manifiesto): no se puede adivinar ni falsificar. Sin URL publica no hay enlace util.
        var urlPublica = aplicacion.Value.UrlPublica?.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(urlPublica))
            throw new clsServiceUnavailableException(
                "APP_URL_PUBLICA_NO_CONFIGURADA", "Falta la URL pública de la aplicación (App__UrlPublica) para armar el enlace de la bitácora.");
        var bitacoraUrl = $"{urlPublica}/bitacora/{tokens.Emitir(autorizacion.Id)}";

        var mapaUrl = clsOrdenTanque.ArmarMapaUrl(estacion.Lat, estacion.Lng);
        var ruta = viaje.Ruta is { Length: > 0 } ? viaje.Ruta : estacion.Ruta;
        var texto = clsOrdenTanque.ArmarTexto(new clsDatosOrdenTanqueo(
            estacion.Nombre, autorizacion.GalonesMax, vehiculo.Placa, viaje.Manifiesto, ruta, mapaUrl, bitacoraUrl));

        // Envia solo si la API esta configurada; si no, deja la orden preparada (enviado = false) sin fallar la peticion.
        clsWhatsappResultadoDto resultado;
        if (whatsapp.EstaConfigurado)
        {
            try
            {
                var sid = await whatsapp.EnviarAsync(destinatario.Telefono, texto, ct);
                resultado = new clsWhatsappResultadoDto(true, sid, null);
            }
            catch (clsWhatsappEnvioException ex)
            {
                resultado = new clsWhatsappResultadoDto(false, null, ex.Message);
            }
        }
        else
        {
            resultado = new clsWhatsappResultadoDto(false, null, "WhatsApp no configurado (Whatsapp__ApiUrl y Whatsapp__ApiKey).");
        }

        // El celular se enmascara en la auditoria y el enlace (credencial de la bitacora) no se registra.
        await auditoria.RegistrarAsync(
            usuarioActual.UsuarioId, "notificar_conductor", "autorizaciones_tanqueo", autorizacion.Id,
            new
            {
                whatsapp_enviado = resultado.Enviado,
                destinatario = Enmascarar(destinatario.Telefono),
                modo_prueba = whatsapp.ModoPrueba,
            }, ct);

        return new clsNotificarDto(
            autorizacion.Id,
            new clsDestinatarioDto(destinatario.Nombre, destinatario.Telefono),
            new clsOrdenTanqueoDto(vehiculo.Placa, viaje.Manifiesto, ruta, estacion.Nombre, autorizacion.GalonesMax, mapaUrl),
            bitacoraUrl,
            resultado);
    }

    /// <summary>
    /// Con modo prueba, TODAS las ordenes van al numero de pruebas configurado; si no, al celular del conductor del viaje
    /// (normalizado a +57...). Sin celular valido no se envia: 409 CONDUCTOR_SIN_CELULAR.
    /// </summary>
    private async Task<clsDestinatarioDto> ResolverDestinatarioAsync(string? cedulaConductor, CancellationToken ct)
    {
        if (whatsapp.ModoPrueba)
        {
            var numeroPrueba = clsTexto.NormalizarCelularCo(whatsapp.NumeroPrueba)
                ?? throw new clsServiceUnavailableException(
                    "WHATSAPP_NUMERO_PRUEBA_INVALIDO",
                    "El modo de prueba de WhatsApp está activo pero Whatsapp__NumeroPrueba no es un celular colombiano válido.");
            return new clsDestinatarioDto(NOMBRE_DESTINATARIO_GENERICO, numeroPrueba);
        }

        var conductor = string.IsNullOrWhiteSpace(cedulaConductor)
            ? null
            : await contexto.ObtenerConductorPorCedulaAsync(cedulaConductor, ct);
        var celular = clsTexto.NormalizarCelularCo(conductor?.NumeroCelular)
            ?? throw new clsConflictException("CONDUCTOR_SIN_CELULAR", "El conductor no tiene un celular válido registrado.");
        return new clsDestinatarioDto(
            clsTexto.NullifyBlank(conductor?.Nombres) ?? NOMBRE_DESTINATARIO_GENERICO, celular);
    }

    private static string Enmascarar(string telefono) =>
        telefono.Length <= 4 ? "****" : $"{new string('*', telefono.Length - 4)}{telefono[^4..]}";
}
