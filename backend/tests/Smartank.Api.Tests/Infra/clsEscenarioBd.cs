using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Precios.Entidades;
using Smartank.Domain.Rutas.Entidades;
using Smartank.Domain.Tanqueos.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Api.Tests.Infra;

/// <summary>
/// Escenario de datos para pruebas contra SQL Server: todo ocurre dentro de UNA transaccion que se revierte al terminar
/// (DisposeAsync), de modo que la base de pruebas queda intacta aunque otras pruebas la usen a la vez. Las filas se
/// siembran con marcas unicas (placas y rutas con un sufijo aleatorio) para que no choquen con datos existentes.
/// </summary>
public sealed class clsEscenarioBd : IAsyncDisposable
{
    private readonly IDbContextTransaction _transaccion;

    private clsEscenarioBd(clsSmartankDbContext db, IDbContextTransaction transaccion)
    {
        Db = db;
        _transaccion = transaccion;
    }

    public clsSmartankDbContext Db { get; }

    /// <summary>Tres letras aleatorias: hacen unicas las placas y las rutas del escenario (17.576 combinaciones).</summary>
    public string Marca { get; } = new(Enumerable.Range(0, 3).Select(_ => (char)('A' + Random.Shared.Next(26))).ToArray());

    public static async Task<clsEscenarioBd> CrearAsync()
    {
        var db = clsContextoDePruebas.Crear();
        var transaccion = await db.Database.BeginTransactionAsync();
        return new clsEscenarioBd(db, transaccion);
    }

    /// <summary>Placa valida (3 letras + 3 digitos) unica dentro del escenario.</summary>
    public string Placa(int n) => $"{Marca}{n:000}";

    public async Task<clsVehiculo> AgregarVehiculoAsync(string placa, Action<clsVehiculo>? ajustar = null)
    {
        var v = new clsVehiculo { Placa = placa, TipoMotor = "diesel" };
        ajustar?.Invoke(v);
        Db.Vehiculos.Add(v);
        await Db.SaveChangesAsync();
        return v;
    }

    public async Task<clsViaje> AgregarViajeAsync(Guid vehiculoId, string ruta, Action<clsViaje>? ajustar = null)
    {
        var v = new clsViaje { VehiculoId = vehiculoId, Ruta = ruta };
        ajustar?.Invoke(v);
        Db.Viajes.Add(v);
        await Db.SaveChangesAsync();
        return v;
    }

    /// <summary>Ruta con polilinea; el trigger de la base deriva origen y destino del primer y ultimo punto.</summary>
    public async Task<clsRuta> AgregarRutaAsync(Guid viajeId, double[][] polilinea, double kilometros)
    {
        var r = new clsRuta
        {
            Fo = Guid.NewGuid().ToString("N")[..20],
            ViajeId = viajeId,
            Kilometros = kilometros,
            NumPuntos = polilinea.Length,
            Polilinea = polilinea,
            FechaCalculo = DateTimeOffset.UtcNow,
        };
        Db.Rutas.Add(r);
        await Db.SaveChangesAsync();
        return r;
    }

    public async Task<clsEstacion> AgregarEstacionAsync(string nombre, Action<clsEstacion>? ajustar = null)
    {
        var e = new clsEstacion { Nombre = nombre };
        ajustar?.Invoke(e);
        Db.Estaciones.Add(e);
        await Db.SaveChangesAsync();
        return e;
    }

    public async Task<clsAutorizacionTanqueo> AgregarAutorizacionAsync(
        Guid viajeId, Guid vehiculoId, Guid estacionId, EstadoAutorizacion estado)
    {
        var a = new clsAutorizacionTanqueo
        {
            ViajeId = viajeId,
            VehiculoId = vehiculoId,
            EstacionId = estacionId,
            GalonesMax = 40m,
            PrecioTecho = 9300m,
            FechaInicio = DateTimeOffset.UtcNow,
            FechaFin = DateTimeOffset.UtcNow.AddHours(12),
            Estado = estado,
        };
        Db.AutorizacionesTanqueo.Add(a);
        await Db.SaveChangesAsync();
        return a;
    }

    public async Task<clsTanqueo> AgregarTanqueoAsync(
        string placa, DateTimeOffset fecha, decimal galones, decimal valorGalon, string estacion,
        FuenteTanqueo fuente = FuenteTanqueo.Terpel, string producto = "DIESEL")
    {
        var t = new clsTanqueo
        {
            Fuente = fuente,
            FuenteId = Random.Shared.NextInt64(1, long.MaxValue),
            Placa = placa,
            Fecha = fecha,
            Galones = galones,
            ValorGalon = valorGalon,
            ValorTotal = galones * valorGalon,
            Producto = producto,
            Ciudad = "Bogota",
            Estacion = estacion,
        };
        Db.Tanqueos.Add(t);
        await Db.SaveChangesAsync();
        return t;
    }

    public async Task AgregarPrecioRefAsync(Guid viajeId, decimal? min, decimal? max)
    {
        Db.ViajePreciosRef.Add(new clsViajePrecioRef { ViajeId = viajeId, AcpmMin = min, AcpmMax = max, EdsCerca = 2, CalculadoAt = DateTimeOffset.UtcNow });
        await Db.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _transaccion.RollbackAsync();
        await _transaccion.DisposeAsync();
        await Db.DisposeAsync();
    }
}
