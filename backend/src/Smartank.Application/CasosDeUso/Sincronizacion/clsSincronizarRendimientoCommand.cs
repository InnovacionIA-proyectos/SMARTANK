using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Deriva el rendimiento real (km/galon) de cada vehiculo con los contadores acumulados de Geotab (odometro y combustible
/// total) en la ventana <c>Sync__RendimientoVentanaDias</c> y lo guarda como rendimiento historico, que la autonomia
/// prioriza. <paramref name="VentanaDias"/> null usa la configurada. Idempotente.
/// </summary>
public sealed record clsSincronizarRendimientoCommand(int? VentanaDias = null) : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarRendimientoCommandHandler(
    IGeotabPort geotab,
    IVehiculosSincronizacionRepository vehiculos,
    IOptions<clsSyncOptions> opciones,
    TimeProvider reloj,
    ILogger<clsSincronizarRendimientoCommandHandler> log)
    : ICommandHandler<clsSincronizarRendimientoCommand, clsResultadoSincronizacion>
{
    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarRendimientoCommand command, CancellationToken ct = default)
    {
        var ventanaDias = command.VentanaDias is > 0 ? command.VentanaDias.Value : opciones.Value.RendimientoVentanaDiasEfectiva;
        var desde = reloj.GetUtcNow().AddDays(-ventanaDias);

        var tareaDispositivos = geotab.ListarDispositivosAsync(ct);
        var tareaCombustible = geotab.ObtenerCombustibleUsadoAsync(desde, ct);
        var tareaOdometro = geotab.ObtenerOdometroAsync(desde, ct);
        var tareaVehiculos = vehiculos.ObtenerMapaPlacaAIdAsync(ct);
        await Task.WhenAll(tareaDispositivos, tareaCombustible, tareaOdometro, tareaVehiculos);
        var dispositivos = tareaDispositivos.Result;
        var placaAId = tareaVehiculos.Result;

        var dispositivoAPlaca = new Dictionary<string, string>();
        foreach (var d in dispositivos)
        {
            var placa = !string.IsNullOrWhiteSpace(d.Nombre) ? d.Nombre : d.Placa;
            if (!string.IsNullOrWhiteSpace(placa)) dispositivoAPlaca[d.Id] = placa.Trim().ToUpperInvariant();
        }

        var combustible = AgruparPorDispositivo(tareaCombustible.Result);
        var odometro = AgruparPorDispositivo(tareaOdometro.Result);

        var sinDatos = 0;
        var fueraDeBanda = 0;
        var sinVehiculo = 0;
        var filas = new List<clsRendimientoVehiculoDto>();
        var placaPorVehiculo = new Dictionary<Guid, string>();
        foreach (var (dispositivoId, rangoCombustible) in combustible)
        {
            if (!dispositivoAPlaca.TryGetValue(dispositivoId, out var placa) || !placaAId.TryGetValue(placa, out var vehiculoId))
            {
                sinVehiculo++;
                continue;
            }
            if (!odometro.TryGetValue(dispositivoId, out var rangoOdometro))
            {
                sinDatos++;
                continue;
            }

            var calculo = clsRendimiento.Calcular(
                rangoCombustible.Inicial, rangoCombustible.Final, rangoOdometro.Inicial, rangoOdometro.Final);
            switch (calculo.Resultado)
            {
                case ResultadoRendimiento.SinDatos:
                    sinDatos++;
                    break;
                case ResultadoRendimiento.FueraDeBanda:
                    fueraDeBanda++;
                    break;
                default:
                    placaPorVehiculo[vehiculoId] = placa;
                    filas.Add(new clsRendimientoVehiculoDto(vehiculoId, calculo.KmPorGalon!.Value));
                    break;
            }
        }

        var resultado = await vehiculos.ActualizarRendimientoAsync(filas, ct);
        var errores = resultado.Errores
            .Select(e => Guid.TryParse(e.Clave, out var id) && placaPorVehiculo.TryGetValue(id, out var p) ? e with { Clave = p } : e)
            .ToList();

        log.LogInformation(
            "Sincronizacion de rendimiento ({Ventana} dias): {Actualizados} actualizados, {SinDatos} sin datos suficientes, {FueraDeBanda} fuera de banda, {SinVehiculo} sin vehiculo, {Errores} errores",
            ventanaDias, resultado.Actualizados, sinDatos, fueraDeBanda, sinVehiculo, errores.Count);

        return clsResultadoSincronizacion.Crear(
            dispositivos.Count, 0, resultado.Actualizados, sinDatos + fueraDeBanda + sinVehiculo, errores,
            new Dictionary<string, int>
            {
                ["sinDatos"] = sinDatos,
                ["fueraDeBanda"] = fueraDeBanda,
                ["sinVehiculo"] = sinVehiculo,
                ["ventanaDias"] = ventanaDias,
            });
    }

    private static Dictionary<string, clsRangoContador> AgruparPorDispositivo(IReadOnlyList<clsGeotabLecturaDto> lecturas)
    {
        var rangos = new Dictionary<string, clsRangoContador>();
        foreach (var l in lecturas)
        {
            if (!rangos.TryGetValue(l.DispositivoId, out var rango)) rangos[l.DispositivoId] = rango = new clsRangoContador();
            rango.Agregar(l.Valor, l.FechaUtc);
        }
        return rangos;
    }
}
