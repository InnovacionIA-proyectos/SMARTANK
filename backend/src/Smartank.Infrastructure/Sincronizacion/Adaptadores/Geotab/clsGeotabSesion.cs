namespace Smartank.Infrastructure.Sincronizacion.Adaptadores.Geotab;

/// <summary>Sesion autenticada de Geotab: credenciales de sesion y host efectivo tras la federacion (p. ej. "my379.geotab.com").</summary>
public sealed record clsGeotabSesion(string Database, string UserName, string SessionId, string Servidor);
