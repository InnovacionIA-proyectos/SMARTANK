namespace Smartank.Application.CasosDeUso.Metricas.DTOs;

/// <summary>Filtros de la consulta de recuperacion; tambien se devuelven tal como se aplicaron.</summary>
/// <param name="Programa">Programa de flota del vehiculo.</param>
/// <param name="Proveedor">Red del historial de tanqueos (terpel / distracom).</param>
/// <param name="Producto">Producto tanqueado.</param>
/// <param name="Anio">Anio de la fecha del tanqueo.</param>
/// <param name="Mes">Mes 1-12 de la fecha del tanqueo.</param>
/// <param name="Ruta">Ruta del viaje al que se liga el tanqueo.</param>
public record clsRecuperacionFiltrosDto(
    string? Programa,
    string? Proveedor,
    string? Producto,
    int? Anio,
    int? Mes,
    string? Ruta);

public record clsUtilizacionEdsDto(string Proveedor, int Tanqueos, decimal Galones);

public record clsEquipoFilaDto(
    string Equipo,
    int Mes,
    DateTimeOffset Fecha,
    decimal ValorGalon,
    decimal Galones,
    decimal ValorTotal,
    string Eds,
    string? Ciudad);

public record clsGalonesPorEdsDto(
    Guid Id,
    string Nombre,
    double Lat,
    double Lng,
    string? Ruta,
    string? Proveedor,
    decimal? Precio,
    decimal Galones,
    int Tanqueos);

public record clsRutaLineaDto(string? Ruta, double OrigenLat, double OrigenLng, double DestinoLat, double DestinoLng);

/// <summary>Valores distintos para poblar los desplegables de filtro (no dependen de los filtros aplicados).</summary>
public record clsOpcionesFiltroDto(
    IReadOnlyList<string> Programas,
    IReadOnlyList<string> Proveedores,
    IReadOnlyList<string> Productos,
    IReadOnlyList<int> Anios,
    IReadOnlyList<int> Meses,
    IReadOnlyList<string> Rutas);

/// <summary>Respuesta de GET /api/metricas/recuperacion (forma <c>MetricasResponse</c> del origen).</summary>
public record clsMetricasRecuperacionDto(
    clsRecuperacionFiltrosDto Filtros,
    decimal GalonesTotal,
    int TanqueosTotal,
    decimal GastoReal,
    decimal GastoIdeal,
    decimal GastoMaximo,
    decimal AhorroVsMaximo,
    decimal OportunidadVsIdeal,
    decimal? PrecioMinimoRuta,
    IReadOnlyList<clsUtilizacionEdsDto> UtilizacionEds,
    IReadOnlyList<clsEquipoFilaDto> Equipos,
    IReadOnlyList<clsGalonesPorEdsDto> GalonesPorEds,
    IReadOnlyList<clsRutaLineaDto> Rutas,
    clsOpcionesFiltroDto Opciones);
