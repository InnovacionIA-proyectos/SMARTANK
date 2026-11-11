using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Telemetria desde Geotab a los vehiculos: ultima posicion, nivel de tanque en galones (porcentaje x capacidad), odometro
/// en km y enlace dispositivo-placa. El nivel se busca en la ventana <c>Sync__VentanaNivelHoras</c>. Idempotente.
/// </summary>
public sealed record clsSincronizarTelemetriaCommand : ICommand<clsResultadoSincronizacion>;

public sealed class clsSincronizarTelemetriaCommandHandler(
    IGeotabPort geotab,
    IVehiculosSincronizacionRepository vehiculos,
    IOptions<clsSyncOptions> opciones,
    TimeProvider reloj,
    ILogger<clsSincronizarTelemetriaCommandHandler> log)
    : ICommandHandler<clsSincronizarTelemetriaCommand, clsResultadoSincronizacion>
{
    private sealed class clsAcumulado(string dispositivoId)
    {
        public string DispositivoId { get; } = dispositivoId;
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public DateTimeOffset? UbicacionAt { get; set; }
        public decimal? NivelGalones { get; set; }
        public DateTimeOffset? NivelAt { get; set; }
        public double? OdometroKm { get; set; }
        public bool ConUbicacion { get; set; }
        public bool ConNivel { get; set; }
    }

    private static string Normalizar(string placa) => placa.Trim().ToUpperInvariant();

    public async Task<clsResultadoSincronizacion> Handle(clsSincronizarTelemetriaCommand command, CancellationToken ct = default)
    {
        var ventanaHoras = opciones.Value.VentanaNivelHorasEfectiva;
        var desde = reloj.GetUtcNow().AddHours(-ventanaHoras);

        var tareaDispositivos = geotab.ListarDispositivosAsync(ct);
        var tareaEstados = geotab.ObtenerEstadoDispositivosAsync(ct);
        var tareaNivel = geotab.ObtenerNivelCombustibleAsync(desde, ct);
        var tareaVehiculos = vehiculos.ListarBasicosAsync(ct);
        var tareaOdometro = ObtenerOdometroOpcionalAsync(desde, ct);
        await Task.WhenAll(tareaDispositivos, tareaEstados, tareaNivel, tareaVehiculos, tareaOdometro);
        var dispositivos = tareaDispositivos.Result;
        var estados = tareaEstados.Result;
        var nivel = tareaNivel.Result;
        var flota = tareaVehiculos.Result;
        var odometro = tareaOdometro.Result;

        // device.id -> placa normalizada (mayusculas, sin espacios laterales). El nombre del dispositivo ES la placa.
        var dispositivoAPlaca = new Dictionary<string, string>();
        foreach (var d in dispositivos)
        {
            var placa = !string.IsNullOrWhiteSpace(d.Nombre) ? d.Nombre : d.Placa;
            if (!string.IsNullOrWhiteSpace(placa)) dispositivoAPlaca[d.Id] = Normalizar(placa);
        }

        var vehiculoPorPlaca = new Dictionary<string, clsVehiculoBasicoDto>();
        foreach (var v in flota) vehiculoPorPlaca[Normalizar(v.Placa)] = v;

        var acumulados = new Dictionary<string, clsAcumulado>();
        clsAcumulado Asegurar(string placa, string dispositivoId)
        {
            if (!acumulados.TryGetValue(placa, out var a)) acumulados[placa] = a = new clsAcumulado(dispositivoId);
            return a;
        }

        // Ubicacion: se ignoran lecturas sin coordenadas, el (0,0) de un GPS sin senal y las fuera de rango.
        foreach (var s in estados)
        {
            if (!dispositivoAPlaca.TryGetValue(s.DispositivoId, out var placa)) continue;
            if (s.Latitud is not { } lat || s.Longitud is not { } lng) continue;
            if (lat == 0 && lng == 0) continue;
            if (Math.Abs(lat) > 90 || Math.Abs(lng) > 180) continue;
            var a = Asegurar(placa, s.DispositivoId);
            a.Lat = lat;
            a.Lng = lng;
            a.UbicacionAt = s.FechaUtc;
            a.ConUbicacion = true;
        }

        // Nivel: la ultima lectura de cada dispositivo en la ventana, convertida de % a galones con la capacidad del vehiculo.
        foreach (var (dispositivoId, lectura) in UltimaLecturaPorDispositivo(nivel))
        {
            if (!dispositivoAPlaca.TryGetValue(dispositivoId, out var placa)) continue;
            if (!vehiculoPorPlaca.TryGetValue(placa, out var vehiculo)) continue;
            if (vehiculo.CapacidadTanqueGal is not > 0) continue;   // sin capacidad no se puede convertir
            if (lectura.Valor < 0) continue;
            var a = Asegurar(placa, dispositivoId);
            a.NivelGalones = clsNivelCombustible.Galones(lectura.Valor, vehiculo.CapacidadTanqueGal.Value);
            a.NivelAt = lectura.FechaUtc;
            a.ConNivel = true;
        }

        // Odometro: ultima lectura de cada dispositivo (metros) a km.
        foreach (var (dispositivoId, lectura) in UltimaLecturaPorDispositivo(odometro))
        {
            if (!dispositivoAPlaca.TryGetValue(dispositivoId, out var placa)) continue;
            if (!vehiculoPorPlaca.ContainsKey(placa) || lectura.Valor < 0) continue;
            Asegurar(placa, dispositivoId).OdometroKm = clsNivelCombustible.OdometroKm(lectura.Valor);
        }

        var sinVehiculo = 0;
        var filas = new List<clsActualizacionTelemetriaDto>();
        var porVehiculo = new Dictionary<Guid, clsAcumulado>();
        foreach (var (placa, a) in acumulados)
        {
            if (!vehiculoPorPlaca.TryGetValue(placa, out var vehiculo))
            {
                sinVehiculo++;
                continue;
            }
            porVehiculo[vehiculo.Id] = a;
            filas.Add(new clsActualizacionTelemetriaDto(
                vehiculo.Id, a.Lat, a.Lng, a.UbicacionAt, a.NivelGalones, a.NivelAt, a.OdometroKm, a.DispositivoId));
        }

        var resultado = await vehiculos.ActualizarTelemetriaAsync(filas, ct);

        // El repositorio identifica la fila fallida por el id del vehiculo; para el detalle se muestra la placa.
        var fallidos = resultado.Errores.Select(e => e.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var placaPorId = flota.ToDictionary(v => v.Id, v => v.Placa);
        var errores = resultado.Errores
            .Select(e => Guid.TryParse(e.Clave, out var id) && placaPorId.TryGetValue(id, out var p) ? e with { Clave = p } : e)
            .ToList();
        var conUbicacion = porVehiculo.Count(p => p.Value.ConUbicacion && !fallidos.Contains(p.Key.ToString()));
        var conNivel = porVehiculo.Count(p => p.Value.ConNivel && !fallidos.Contains(p.Key.ToString()));

        log.LogInformation(
            "Sincronizacion de telemetria: {Dispositivos} dispositivos, {Actualizados} vehiculos actualizados ({ConUbicacion} con ubicacion, {ConNivel} con nivel), {SinVehiculo} sin vehiculo, {Errores} errores",
            dispositivos.Count, resultado.Actualizados, conUbicacion, conNivel, sinVehiculo, errores.Count);

        return clsResultadoSincronizacion.Crear(
            dispositivos.Count, 0, resultado.Actualizados, sinVehiculo, errores,
            new Dictionary<string, int>
            {
                ["conUbicacion"] = conUbicacion,
                ["conNivel"] = conNivel,
                ["ventanaNivelHoras"] = ventanaHoras,
            });
    }

    /// <summary>
    /// El odometro es un agregado respecto del comportamiento original: si Geotab falla solo en esa consulta no se pierde la
    /// posicion y el nivel, que si son criticos.
    /// </summary>
    private async Task<IReadOnlyList<clsGeotabLecturaDto>> ObtenerOdometroOpcionalAsync(DateTimeOffset desde, CancellationToken ct)
    {
        try
        {
            return await geotab.ObtenerOdometroAsync(desde, ct);
        }
        // La falta de configuracion no es un fallo parcial: debe llegar al orquestador para omitir el job completo.
        catch (Exception ex) when (ex is not OperationCanceledException
                                   && ex is not clsServiceUnavailableException { Code: "GEOTAB_NO_CONFIGURADO" })
        {
            log.LogWarning("No se pudo leer el odometro de Geotab ({Motivo}); se sincroniza sin odometro", ex.Message);
            return [];
        }
    }

    private static Dictionary<string, clsGeotabLecturaDto> UltimaLecturaPorDispositivo(IReadOnlyList<clsGeotabLecturaDto> lecturas)
    {
        var ultimas = new Dictionary<string, clsGeotabLecturaDto>();
        foreach (var l in lecturas)
            if (!ultimas.TryGetValue(l.DispositivoId, out var previa) || l.FechaUtc > previa.FechaUtc) ultimas[l.DispositivoId] = l;
        return ultimas;
    }
}
