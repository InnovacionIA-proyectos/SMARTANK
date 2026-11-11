using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autonomia;
using Smartank.Application.CasosDeUso.Autonomia.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Autorizaciones.Mappers;
using Smartank.Application.CasosDeUso.Recomendacion.DTOs;
using Smartank.Application.CasosDeUso.Recomendacion.Interfaces;
using Smartank.Application.CasosDeUso.Recomendacion.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Enums;
using Smartank.Domain.Geo;

namespace Smartank.Application.CasosDeUso.Recomendacion;

/// <summary>
/// Recomienda la EDS mas barata donde tanquear, fija galones y precio techo y deja la autorizacion en estado Propuesta.
/// Si el viaje ya tiene una propuesta, la ACTUALIZA en vez de insertar otra (la base solo permite una por viaje).
/// </summary>
/// <param name="ViajeId">Viaje a recomendar.</param>
/// <param name="GalonesSolicitados">Galones explicitos; si no vienen, se calculan para completar la ruta.</param>
public sealed record clsRecomendarTanqueoCommand(Guid ViajeId, decimal? GalonesSolicitados = null) : ICommand<clsRecomendacionDto>;

public sealed class clsRecomendarTanqueoCommandHandler(
    ICommandHandler<clsCalcularAutonomiaCommand, clsAutonomiaDto> calcularAutonomia,
    IContextoTanqueoRepository contexto,
    IRecomendacionRepository recomendacion,
    IAutorizacionesRepository autorizaciones,
    IAuditoriaPort auditoria,
    IUnitOfWork uow,
    ICurrentUserPort usuarioActual,
    IOptions<clsParametrosNegocio> parametros,
    TimeProvider reloj)
    : ICommandHandler<clsRecomendarTanqueoCommand, clsRecomendacionDto>
{
    public async Task<clsRecomendacionDto> Handle(clsRecomendarTanqueoCommand command, CancellationToken ct = default)
    {
        if (command.GalonesSolicitados is <= 0)
            throw new clsValidationFailedException("GALONES_INVALIDOS", "Los galones deben ser mayores que cero.");

        var viaje = await contexto.ObtenerViajeAsync(command.ViajeId, ct)
            ?? throw new clsNotFoundException("VIAJE_NO_ENCONTRADO", "Viaje no encontrado.");

        // Asegura la autonomia (la calcula y persiste si el viaje todavia no la tiene).
        var autonomiaKm = viaje.AutonomiaEstimadaKm
            ?? (await calcularAutonomia.Handle(new clsCalcularAutonomiaCommand(viaje.Id), ct)).AutonomiaKm;

        var estaciones = await recomendacion.ListarEstacionesConPrecioAcpmAsync(ct);
        if (estaciones.Count == 0)
            throw new clsConflictException(
                "SIN_ESTACIONES_CON_PRECIO", "No hay estaciones con precio de ACPM y coordenadas cargados.");

        var polilinea = AClsCoordenadas(await recomendacion.ObtenerPolilineaDeViajeAsync(viaje.Id, ct));
        var vehiculo = await contexto.ObtenerVehiculoAsync(viaje.VehiculoId, ct);
        clsCoordenada? posicion = vehiculo is { UltimaLat: { } lat, UltimaLng: { } lng } ? new clsCoordenada(lat, lng) : null;

        var candidatas = estaciones
            .Select(e => new clsEstacionCandidata(e.Id, e.Lat!.Value, e.Lng!.Value, e.PrecioAcpm!.Value))
            .ToList();
        var seleccion = clsRecomendacionReglas.Seleccionar(candidatas, polilinea, posicion, autonomiaKm);

        var p = parametros.Value;
        var galones = clsRecomendacionReglas.CalcularGalones(
            command.GalonesSolicitados,
            seleccion.RutaRestanteKm,
            autonomiaKm,
            vehiculo is null
                ? null
                : new clsVehiculoParaGalones(
                    vehiculo.RendimientoHistorico, vehiculo.RendimientoEsperado, vehiculo.ToleranciaMotor,
                    vehiculo.CapacidadTanqueGal, vehiculo.NivelTanqueActual),
            p.RendimientoDefaultKmGal,
            p.DefaultGalonesAutorizacion);

        var (elegida, masCara) = clsRecomendacionReglas.ElegirMasBarata(seleccion.Consideradas);
        var ahorro = clsRecomendacionReglas.CalcularAhorro(masCara.PrecioAcpm, elegida.PrecioAcpm, galones);

        var ahora = reloj.GetUtcNow();
        var autorizacion = await GuardarPropuestaAsync(
            viaje.Id, viaje.VehiculoId, elegida, galones, ahorro, ahora, ahora.AddHours(clsRecomendacionReglas.VIGENCIA_HORAS), ct);

        var estacionElegida = estaciones.First(e => e.Id == elegida.Id);
        return new clsRecomendacionDto(
            seleccion.RequiereTanqueo,
            clsAutonomia.Redondear(autonomiaKm, 2),
            seleccion.RutaRestanteKm is { } restante ? clsAutonomia.Redondear(restante, 1) : null,
            clsRecomendacionReglas.MotivoSinTanqueo(seleccion.RequiereTanqueo, autonomiaKm, seleccion.RutaRestanteKm),
            clsAutorizacionMapper.ToDto(autorizacion),
            clsEstacionElegidaMapper.ToDto(estacionElegida),
            galones,
            elegida.PrecioAcpm,
            ahorro,
            seleccion.Consideradas.Count,
            seleccion.SobreRuta);
    }

    /// <summary>
    /// Crea la propuesta del viaje o, si ya hay una, la actualiza. Ante una carrera (otro usuario inserto la propuesta entre la
    /// lectura y el guardado, REGISTRO_DUPLICADO) relee la ganadora y la actualiza.
    /// </summary>
    private async Task<clsAutorizacionTanqueo> GuardarPropuestaAsync(
        Guid viajeId, Guid vehiculoId, clsEstacionCandidata elegida, decimal galones, decimal ahorro,
        DateTimeOffset inicio, DateTimeOffset fin, CancellationToken ct)
    {
        var existente = await autorizaciones.ObtenerPropuestaDeViajeAsync(viajeId, ct);
        if (existente is not null)
        {
            Aplicar(existente, elegida, galones, ahorro, inicio, fin);
            await auditoria.RegistrarAsync(
                usuarioActual.UsuarioId, "recomendar_tanqueo", "autorizaciones_tanqueo", existente.Id,
                new { estado = "propuesta", actualizada = true }, ct);
            await uow.SaveChangesAsync(ct);
            return existente;
        }

        var nueva = new clsAutorizacionTanqueo { ViajeId = viajeId, VehiculoId = vehiculoId, Estado = EstadoAutorizacion.Propuesta };
        Aplicar(nueva, elegida, galones, ahorro, inicio, fin);
        autorizaciones.Agregar(nueva);
        try
        {
            await uow.SaveChangesAsync(ct);
        }
        catch (clsConflictException ex) when (ex.Code == "REGISTRO_DUPLICADO")
        {
            // La insertada fallo: se deja de seguirla para que el guardado siguiente no reintente el INSERT.
            autorizaciones.Descartar(nueva);
            var ganadora = await autorizaciones.ObtenerPropuestaDeViajeAsync(viajeId, ct);
            if (ganadora is null) throw; // el duplicado no era la propuesta del viaje: no es una carrera que se pueda resolver aqui.
            Aplicar(ganadora, elegida, galones, ahorro, inicio, fin);
            await auditoria.RegistrarAsync(
                usuarioActual.UsuarioId, "recomendar_tanqueo", "autorizaciones_tanqueo", ganadora.Id,
                new { estado = "propuesta", actualizada = true }, ct);
            await uow.SaveChangesAsync(ct);
            return ganadora;
        }

        await auditoria.RegistrarAsync(
            usuarioActual.UsuarioId, "recomendar_tanqueo", "autorizaciones_tanqueo", nueva.Id,
            new { estado = "propuesta", actualizada = false }, ct);
        return nueva;
    }

    private static void Aplicar(
        clsAutorizacionTanqueo a, clsEstacionCandidata elegida, decimal galones, decimal ahorro,
        DateTimeOffset inicio, DateTimeOffset fin)
    {
        a.EstacionId = elegida.Id;
        a.GalonesMax = galones;
        a.PrecioTecho = elegida.PrecioAcpm;
        a.DeltaAhorroEstimado = ahorro;
        a.FechaInicio = inicio;
        a.FechaFin = fin;
    }

    /// <summary>Convierte [[lat,lng],...] en coordenadas; ignora puntos mal formados. Null si no hay ruta.</summary>
    private static IReadOnlyList<clsCoordenada>? AClsCoordenadas(double[][]? polilinea) =>
        polilinea?.Where(pt => pt.Length >= 2).Select(pt => new clsCoordenada(pt[0], pt[1])).ToList();
}
