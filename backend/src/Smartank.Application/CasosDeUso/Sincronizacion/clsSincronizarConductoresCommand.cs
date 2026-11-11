using Microsoft.Extensions.Logging;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Domain.Common;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Sincroniza los conductores del legado (upsert por cedula) y les asigna el celular de la app movil cruzado por documento.
/// El celular se normaliza a +57...; si no hay uno valido queda null (nunca se inventa uno). Idempotente.
/// </summary>
public sealed record clsSincronizarConductoresCommand : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarConductoresCommandHandler(
    IConductoresLegadoPort legado,
    IConductoresSincronizacionRepository conductores,
    ILogger<clsSincronizarConductoresCommandHandler> log)
    : ICommandHandler<clsSincronizarConductoresCommand, clsResultadoSincronizacion>
{
    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarConductoresCommand command, CancellationToken ct = default)
    {
        var lista = await legado.ListarAsync(ct);
        var celulares = await legado.ListarCelularesAplMovilAsync(ct);

        var porCedula = new Dictionary<string, clsConductorUpsertDto>(StringComparer.OrdinalIgnoreCase);
        var omitidos = 0;
        var conCelular = 0;
        foreach (var c in lista)
        {
            var cedula = clsTexto.NullifyBlank(c.Cedula);
            if (cedula is null)
            {
                omitidos++;
                continue;
            }

            celulares.TryGetValue(cedula, out var crudo);
            var celular = clsTexto.NormalizarCelularCo(crudo);
            if (celular is not null) conCelular++;

            // Una cedula repetida en el legado dejaria dos filas con la misma llave: gana la ultima y la anterior se cuenta omitida.
            if (porCedula.ContainsKey(cedula)) omitidos++;
            porCedula[cedula] = new clsConductorUpsertDto(
                c.FpsapId, cedula, c.Nombres.Trim(), celular, c.Activo, c.UtilizarTarjetaPrepago);
        }

        var resultado = await conductores.UpsertAsync(porCedula.Values.ToList(), ct);

        log.LogInformation(
            "Sincronizacion de conductores: {Total} leidos, {ConCelular} con celular valido, {Insertados} insertados, {Actualizados} actualizados, {Errores} errores",
            lista.Count, conCelular, resultado.Insertados, resultado.Actualizados, resultado.Errores.Count);

        return clsResultadoSincronizacion.Crear(
            lista.Count, resultado.Insertados, resultado.Actualizados, omitidos, resultado.Errores,
            new Dictionary<string, int> { ["conCelular"] = conCelular });
    }
}
