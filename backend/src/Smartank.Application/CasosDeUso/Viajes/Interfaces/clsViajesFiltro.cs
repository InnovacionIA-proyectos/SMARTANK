using Smartank.Domain.Enums;
using Smartank.Domain.Viajes.Reglas;

namespace Smartank.Application.CasosDeUso.Viajes.Interfaces;

/// <summary>
/// Filtros ya normalizados del listado de viajes; el repositorio los aplica en la base, no en memoria.
/// Los viajes cerrados nunca se listan, con o sin filtros.
/// </summary>
/// <param name="Estado">Solo Programado, EnRuta o Descartado (Cerrado no se lista).</param>
/// <param name="Ruta">Nombre de ruta exacto.</param>
/// <param name="Placa">Fragmento de placa (contiene, sin distinguir mayusculas).</param>
/// <param name="Manifiesto">Fragmento de manifiesto (contiene).</param>
/// <param name="Anio">Anio de la fecha efectiva del viaje (inicio real o, si no hay, fecha de despacho).</param>
/// <param name="Mes">Mes 1-12 de esa misma fecha.</param>
/// <param name="SoloRecomendacionActiva">Solo viajes con una autorizacion de tanqueo vigente (propuesta o autorizada).</param>
/// <param name="TelemetriaReciente">true = nivel leido en la ultima hora; false = sin lectura o mas vieja; null = todos.</param>
/// <param name="UmbralTelemetria">Instante desde el cual una lectura de nivel cuenta como reciente.</param>
public sealed record clsViajesFiltro(
    EstadoViaje? Estado,
    string? Ruta,
    string? Placa,
    string? Manifiesto,
    int? Anio,
    int? Mes,
    TipoDesplazamiento? Desplazamiento,
    bool SoloRecomendacionActiva,
    bool? TelemetriaReciente,
    DateTimeOffset UmbralTelemetria);
