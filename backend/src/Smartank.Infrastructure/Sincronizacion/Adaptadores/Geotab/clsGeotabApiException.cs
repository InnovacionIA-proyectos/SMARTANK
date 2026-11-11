namespace Smartank.Infrastructure.Sincronizacion.Adaptadores.Geotab;

/// <summary>Error que devuelve la propia API de Geotab (campo <c>error</c> del JSON-RPC): sirve para distinguir una sesion invalida de un fallo de red.</summary>
public sealed class clsGeotabApiException(string mensaje) : Exception(mensaje);
