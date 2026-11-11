using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Eventos.Validadores;

/// <summary>
/// Forma del reporte del conductor. Los topes de magnitud evitan que un valor absurdo desborde la columna decimal de la base
/// (y termine en un error 500): decimal(10,3) en galones y niveles, decimal(12,2) en precio, decimal(12,3) en odometro.
/// </summary>
public static class clsEventoValidador
{
    private const decimal MAX_GALONES = 9_999_999m;
    private const decimal MAX_PRECIO = 9_999_999_999m;
    private const double MAX_ODOMETRO = 999_999_999;
    private const int MAX_LONGITUD_URL = 500;

    public static void Validar(
        decimal? galonesCargados, decimal? precioPagado, string? fotoSurtidorUrl,
        double? odometroEvento, decimal? nivelAntes, decimal? nivelDespues)
    {
        if (galonesCargados is null)
            throw Invalido("Faltan los galones cargados.");
        if (galonesCargados is < 0 or > MAX_GALONES)
            throw Invalido("Los galones cargados no pueden ser negativos ni exceder el máximo permitido.");
        if (precioPagado is < 0 or > MAX_PRECIO)
            throw Invalido("El precio pagado no puede ser negativo ni exceder el máximo permitido.");
        if (odometroEvento is < 0 or > MAX_ODOMETRO)
            throw Invalido("El odómetro no puede ser negativo ni exceder el máximo permitido.");
        if (nivelAntes is < 0 or > MAX_GALONES || nivelDespues is < 0 or > MAX_GALONES)
            throw Invalido("Los niveles de tanque no pueden ser negativos ni exceder el máximo permitido.");

        if (!string.IsNullOrWhiteSpace(fotoSurtidorUrl))
        {
            var url = fotoSurtidorUrl.Trim();
            // Solo http(s): la URL se muestra luego a otros usuarios y no debe poder ser, por ejemplo, un javascript:.
            var valida = url.Length <= MAX_LONGITUD_URL
                && Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
            if (!valida)
                throw Invalido("La URL de la foto del surtidor no es válida.");
        }
    }

    private static clsValidationFailedException Invalido(string mensaje) => new("EVENTO_INVALIDO", mensaje);
}
