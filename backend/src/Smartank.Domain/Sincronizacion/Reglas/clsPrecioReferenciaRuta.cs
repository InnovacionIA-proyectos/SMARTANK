namespace Smartank.Domain.Sincronizacion.Reglas;

/// <summary>ACPM minimo y maximo entre las EDS cercanas a una ruta y cuantas EDS cumplieron el umbral (0 = ninguna: min y max nulos).</summary>
public readonly record struct clsPrecioReferenciaRuta(decimal? AcpmMin, decimal? AcpmMax, int EdsCerca);
