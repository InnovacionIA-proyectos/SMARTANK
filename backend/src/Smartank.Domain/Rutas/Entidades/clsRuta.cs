using Smartank.Domain.Enums;

namespace Smartank.Domain.Rutas.Entidades;

/// <summary>Ruta (polilinea) de un FO calculada por el servicio de ruteo (tabla tblRutas).</summary>
public class clsRuta
{
    public Guid Id { get; set; }

    /// <summary>Numero de FO SIN ceros a la izquierda (unico).</summary>
    public string Fo { get; set; } = string.Empty;

    public Guid? ViajeId { get; set; }

    public FuenteRuta Fuente { get; set; } = FuenteRuta.Valhalla;

    public long? ValhallaId { get; set; }

    public double? Kilometros { get; set; }

    public int? NumPuntos { get; set; }

    public int? NumPuntosOriginal { get; set; }

    /// <summary>Polilinea simplificada como [[lat, lng], ...]. Se guarda como JSON; evitar cargarla en listados (proyectar columnas).</summary>
    public double[][]? Polilinea { get; set; }

    /// <summary>Detalle de tramos (costo, tiempo, origen, destino) como texto JSON tal cual lo entrega el servicio de ruteo.</summary>
    public string? DetalleKm { get; set; }

    /// <summary>Primer punto de la polilinea; lo deriva un trigger de la base. Solo lectura.</summary>
    public double? OrigenLat { get; private set; }

    public double? OrigenLng { get; private set; }

    /// <summary>Ultimo punto de la polilinea; lo deriva un trigger de la base. Solo lectura.</summary>
    public double? DestinoLat { get; private set; }

    public double? DestinoLng { get; private set; }

    public DateTimeOffset? FechaCalculo { get; set; }

    public DateTimeOffset? SincronizadoAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
