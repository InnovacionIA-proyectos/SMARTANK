namespace Smartank.Domain.Metricas.Reglas;

/// <summary>Resultado completo del calculo de recuperacion de margen sobre un conjunto de tanqueos.</summary>
public sealed record clsResultadoRecuperacion(
    clsTotalesRecuperacion Totales,
    IReadOnlyList<clsUtilizacionEds> Utilizacion,
    IReadOnlyList<clsEquipoFila> Equipos,
    IReadOnlyList<clsGalonesPorEds> GalonesPorEds,
    IReadOnlyList<clsRutaLinea> Rutas);
