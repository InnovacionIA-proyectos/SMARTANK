namespace Smartank.Domain.Metricas.Reglas;

/// <summary>Tanqueos y galones por red de abastecimiento (proveedor).</summary>
public sealed record clsUtilizacionEds(string Proveedor, int Tanqueos, decimal Galones);
