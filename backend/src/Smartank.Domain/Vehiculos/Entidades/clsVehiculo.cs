namespace Smartank.Domain.Vehiculos.Entidades;

/// <summary>
/// Vehiculo (cabezote) de la flota (tabla tblVehiculos): datos maestros sincronizados, parametros de consumo y
/// ultima telemetria (nivel de tanque y posicion GPS).
/// </summary>
public class clsVehiculo
{
    public Guid Id { get; set; }

    public string Placa { get; set; } = string.Empty;

    public string? AliasPlaca { get; set; }

    /// <summary>Tipo de motor (diesel, gasolina...); base del margen de tolerancia.</summary>
    public string TipoMotor { get; set; } = string.Empty;

    /// <summary>Programa de combustible de la flota ("terpel" o "distracom"); texto libre para admitir programas nuevos.</summary>
    public string? Programa { get; set; }

    public string? ChipTanqueo { get; set; }

    public string? NitPropietario { get; set; }

    /// <summary>Capacidad fisica del tanque, en galones.</summary>
    public decimal? CapacidadTanqueGal { get; set; }

    public decimal? GalonesReserva { get; set; }

    public decimal? GalonesUtilizables { get; set; }

    /// <summary>Tolerancia fisica en galones (distinta de <see cref="ToleranciaMotor"/>, que es una fraccion 0-1).</summary>
    public decimal? GalonesTolerancia { get; set; }

    /// <summary>Rendimiento real medido, en km por galon.</summary>
    public double? RendimientoHistorico { get; set; }

    /// <summary>Rendimiento teorico, en km por galon.</summary>
    public double? RendimientoEsperado { get; set; }

    /// <summary>Fraccion 0-1 de tolerancia sobre el consumo esperado. En la base el valor por defecto es 0.02.</summary>
    public double ToleranciaMotor { get; set; } = 0.02;

    /// <summary>Odometro en kilometros.</summary>
    public double? Odometro { get; set; }

    /// <summary>Ultimo nivel de tanque leido (galones).</summary>
    public decimal? NivelTanqueActual { get; set; }

    /// <summary>Fecha real de la ultima lectura de NIVEL (la de posicion es <see cref="UbicacionAt"/>).</summary>
    public DateTimeOffset? NivelAt { get; set; }

    public double? UltimaLat { get; set; }

    public double? UltimaLng { get; set; }

    public DateTimeOffset? UbicacionAt { get; set; }

    /// <summary>Id del dispositivo en el proveedor de telemetria; unico cuando existe.</summary>
    public string? GeotabDeviceId { get; set; }

    /// <summary>Id del cabezote en el maestro legado; unico cuando existe (los vehiculos creados a mano lo dejan en null).</summary>
    public long? FpsapId { get; set; }

    public DateTimeOffset? FpsapSincronizadoAt { get; set; }

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
