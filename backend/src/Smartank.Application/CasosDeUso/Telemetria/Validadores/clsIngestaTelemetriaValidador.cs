using System.Text.RegularExpressions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Telemetria.Validadores;

/// <summary>Mismas reglas que el esquema de validacion del origen (placa, nivel, odometro, lat, lng, leido_at), con topes que la base soporta.</summary>
public static partial class clsIngestaTelemetriaValidador
{
    /// <summary>La columna NivelTanqueActual es decimal(10,3): un tope sensato evita desbordarla con un dato corrupto.</summary>
    public const decimal NIVEL_MAXIMO_GALONES = 100_000m;

    /// <summary>La columna Odometro es decimal(12,3).</summary>
    public const double ODOMETRO_MAXIMO_KM = 100_000_000;

    /// <summary>Holgura de reloj entre el proveedor y este servidor; una lectura mas adelantada que esto se rechaza.</summary>
    public static readonly TimeSpan TOLERANCIA_RELOJ = TimeSpan.FromMinutes(5);

    /// <summary>Placa colombiana: 3 letras + 2-3 digitos + sufijo opcional (ABC123, ABC12D).</summary>
    [GeneratedRegex("^[A-Z]{3}[0-9]{2,3}[A-Z]?$")]
    private static partial Regex PatronPlaca();

    public static string NormalizarPlaca(string? placa) => (placa ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Valida la lectura y devuelve la placa normalizada (sin espacios, en mayusculas). Lanza 400 con todos los errores encontrados.</summary>
    public static string ValidarYNormalizar(
        string? placa, decimal? nivelGalones, double? odometro, double? lat, double? lng, DateTimeOffset? leidoAt, DateTimeOffset ahora)
    {
        var placaNormalizada = NormalizarPlaca(placa);
        var errores = new List<clsErrorCampo>();

        if (!PatronPlaca().IsMatch(placaNormalizada))
            errores.Add(new("placa", "Placa inválida (formato esperado: ABC123)."));

        if (nivelGalones is null)
            errores.Add(new("nivel_galones", "nivel_galones es requerido."));
        else if (nivelGalones < 0 || nivelGalones > NIVEL_MAXIMO_GALONES)
            errores.Add(new("nivel_galones", $"nivel_galones debe estar entre 0 y {NIVEL_MAXIMO_GALONES:0}."));

        if (odometro is { } o && (!double.IsFinite(o) || o < 0 || o > ODOMETRO_MAXIMO_KM))
            errores.Add(new("odometro", "odometro debe ser un número no negativo."));

        if (lat is { } la && (!double.IsFinite(la) || la is < -90 or > 90))
            errores.Add(new("lat", "lat debe estar entre -90 y 90."));

        if (lng is { } lo && (!double.IsFinite(lo) || lo is < -180 or > 180))
            errores.Add(new("lng", "lng debe estar entre -180 y 180."));

        // Una fecha futura envenenaria el criterio de "telemetria reciente" del listado de viajes.
        if (leidoAt is { } leido && leido > ahora + TOLERANCIA_RELOJ)
            errores.Add(new("leido_at", "leido_at no puede estar en el futuro."));

        if (errores.Count > 0)
            throw new clsValidationFailedException(
                "TELEMETRIA_INVALIDA",
                "Datos de telemetría inválidos: " + string.Join("; ", errores.Select(e => $"{e.Campo}: {e.Mensaje}")),
                errores);

        return placaNormalizada;
    }
}

/// <summary>Un error de validacion asociado al campo del cuerpo que lo causo.</summary>
public record clsErrorCampo(string Campo, string Mensaje);
