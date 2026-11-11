using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autonomia.DTOs;
using Smartank.Application.CasosDeUso.Autonomia.Mappers;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Autorizaciones.Reglas;

namespace Smartank.Application.CasosDeUso.Autonomia;

/// <summary>
/// Calcula la autonomia de un viaje con la ultima lectura de nivel del vehiculo y la persiste en el viaje.
/// Mientras no haya historico del vehiculo usa el rendimiento esperado, y sin ninguno el default de negocio.
/// </summary>
public sealed record clsCalcularAutonomiaCommand(Guid ViajeId) : ICommand<clsAutonomiaDto>;

public sealed class clsCalcularAutonomiaCommandHandler(
    IContextoTanqueoRepository contexto,
    IUnitOfWork uow,
    IOptions<clsParametrosNegocio> parametros,
    TimeProvider reloj)
    : ICommandHandler<clsCalcularAutonomiaCommand, clsAutonomiaDto>
{
    /// <summary>La columna del viaje guarda 2 decimales: se persiste ya redondeado para que lo guardado y lo respondido coincidan.</summary>
    private const int DECIMALES_AUTONOMIA = 2;

    public async Task<clsAutonomiaDto> Handle(clsCalcularAutonomiaCommand command, CancellationToken ct = default)
    {
        var viaje = await contexto.ObtenerViajeAsync(command.ViajeId, ct)
            ?? throw new clsNotFoundException("VIAJE_NO_ENCONTRADO", "Viaje no encontrado.");
        var vehiculo = await contexto.ObtenerVehiculoAsync(viaje.VehiculoId, ct)
            ?? throw new clsNotFoundException("VEHICULO_NO_ENCONTRADO", "Vehículo del viaje no encontrado.");

        if (vehiculo.NivelTanqueActual is not { } nivel)
            throw new clsConflictException(
                "SIN_TELEMETRIA_NIVEL", "El vehículo no tiene telemetría de nivel; no se puede calcular autonomía.");

        // Una lectura vieja da una autonomia irreal (el tanque pudo vaciarse o llenarse desde entonces).
        var p = parametros.Value;
        if (clsAutonomia.EsLecturaObsoleta(vehiculo.NivelAt, reloj.GetUtcNow(), p.NivelMaxAntiguedadHoras))
            throw new clsConflictException(
                "TELEMETRIA_OBSOLETA",
                $"La última lectura de nivel del vehículo tiene más de {p.NivelMaxAntiguedadHoras:0.##} horas; no se puede calcular autonomía.");

        // El maestro de flota no trae rendimiento: el default de negocio es la semilla hasta que haya historico o esperado.
        var rendimiento = clsAutonomia.ResolverRendimiento(
            vehiculo.RendimientoHistorico, vehiculo.RendimientoEsperado, p.RendimientoDefaultKmGal);
        var autonomia = clsAutonomia.Redondear(
            clsAutonomia.CalcularKm((double)nivel, rendimiento.KmPorGalon, vehiculo.ToleranciaMotor), DECIMALES_AUTONOMIA);

        viaje.AutonomiaEstimadaKm = autonomia;
        await uow.SaveChangesAsync(ct);

        return new clsAutonomiaDto(
            clsViajeAutonomiaMapper.ToDto(viaje), autonomia, rendimiento.Base, rendimiento.KmPorGalon, vehiculo.ToleranciaMotor);
    }
}
