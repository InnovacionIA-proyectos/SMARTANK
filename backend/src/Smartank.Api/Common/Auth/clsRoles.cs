using Smartank.Domain.Enums;

namespace Smartank.Api.Common.Auth;

/// <summary>
/// Roles para <c>[Authorize(Roles = ...)]</c>. Cada valor es el NOMBRE del miembro del enum <see cref="Rol"/> (es lo que
/// lleva el claim de rol del token), no el texto snake_case de la base de datos. Las combinaciones son la lista separada
/// por comas que entiende el atributo (basta con tener uno de los roles).
/// </summary>
public static class clsRoles
{
    public const string DESPACHO = nameof(Rol.Despacho);
    public const string CONTROL_FLOTA = nameof(Rol.ControlFlota);
    public const string FINANZAS = nameof(Rol.Finanzas);
    public const string ADMIN = nameof(Rol.Admin);

    /// <summary>Crean y autorizan autorizaciones de tanqueo y notifican al conductor.</summary>
    public const string DESPACHO_ADMIN = DESPACHO + "," + ADMIN;

    /// <summary>Gestionan la bandeja de alertas.</summary>
    public const string CONTROL_FLOTA_ADMIN = CONTROL_FLOTA + "," + ADMIN;

    /// <summary>Consultan las metricas de recuperacion de margen.</summary>
    public const string FINANZAS_ADMIN = FINANZAS + "," + ADMIN;
}
