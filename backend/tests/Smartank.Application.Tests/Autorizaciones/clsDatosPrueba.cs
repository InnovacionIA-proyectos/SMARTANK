using System.Text.Json;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Conductores.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Application.Tests.Autorizaciones;

/// <summary>Fabrica de entidades de prueba con valores razonables; cada test cambia solo lo que le importa.</summary>
public static class clsDatosPrueba
{
    public static readonly DateTimeOffset AHORA = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    public static clsVehiculo Vehiculo(
        decimal? nivel = 100m, double? historico = 5, double? esperado = null, double tolerancia = 0.02,
        decimal? capacidad = 300m, DateTimeOffset? nivelAt = null, double? lat = null, double? lng = null,
        string programa = "TM_MED-CTG SYT", string tipoMotor = "diesel") => new()
    {
        Id = Guid.NewGuid(),
        Placa = "ABC123",
        TipoMotor = tipoMotor,
        Programa = programa,
        NivelTanqueActual = nivel,
        RendimientoHistorico = historico,
        RendimientoEsperado = esperado,
        ToleranciaMotor = tolerancia,
        CapacidadTanqueGal = capacidad,
        NivelAt = nivelAt ?? AHORA.AddMinutes(-10),
        UltimaLat = lat,
        UltimaLng = lng,
    };

    public static clsViaje Viaje(Guid? vehiculoId = null, double? autonomia = null, string? cedula = "80769132") => new()
    {
        Id = Guid.NewGuid(),
        VehiculoId = vehiculoId ?? Guid.NewGuid(),
        Ruta = "Bogota - Medellin",
        Estado = EstadoViaje.EnRuta,
        Manifiesto = "301457403",
        AutonomiaEstimadaKm = autonomia,
        CedulaConductor = cedula,
    };

    public static clsEstacion Estacion(
        double? lat = 4.5, double? lng = -74.1, decimal? precioAcpm = 9300m, string nombre = "EDS Norte",
        bool soportaPreautorizacion = true, string? ruta = "Bogota - Medellin", decimal? precioGalon = null) => new()
    {
        Id = Guid.NewGuid(),
        Nombre = nombre,
        Ruta = ruta,
        Lat = lat,
        Lng = lng,
        PrecioAcpm = precioAcpm,
        PrecioGalonActual = precioGalon ?? precioAcpm,
        SoportaPreautorizacion = soportaPreautorizacion,
        Proveedor = "terpel",
        Producto = "ACPM",
    };

    public static clsConductor Conductor(string? celular = "3106996633", string nombres = "Conductor Uno") => new()
    {
        Id = Guid.NewGuid(),
        Cedula = "80769132",
        Nombres = nombres,
        NumeroCelular = celular,
    };

    public static clsAutorizacionTanqueo Autorizacion(
        EstadoAutorizacion estado = EstadoAutorizacion.Propuesta, decimal galonesMax = 40m, Guid? viajeId = null,
        Guid? vehiculoId = null, Guid? estacionId = null, DateTimeOffset? inicio = null, DateTimeOffset? fin = null) => new()
    {
        Id = Guid.NewGuid(),
        ViajeId = viajeId ?? Guid.NewGuid(),
        VehiculoId = vehiculoId ?? Guid.NewGuid(),
        EstacionId = estacionId ?? Guid.NewGuid(),
        GalonesMax = galonesMax,
        PrecioTecho = 9300m,
        FechaInicio = inicio ?? AHORA.AddHours(-1),
        FechaFin = fin ?? AHORA.AddHours(11),
        Estado = estado,
    };

    private static readonly JsonSerializerOptions JSON = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };

    /// <summary>Serializa a JSON snake_case, igual que el adaptador de auditoria, para comparar diffs.</summary>
    public static string Json(object? valor) => JsonSerializer.Serialize(valor, JSON);
}
