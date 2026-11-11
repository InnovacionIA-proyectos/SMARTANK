namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>Rendimiento derivado: el resultado y, solo si fue <see cref="ResultadoRendimiento.Calculado"/>, los km por galon.</summary>
public readonly record struct clsRendimientoCalculado(ResultadoRendimiento Resultado, double? KmPorGalon);
