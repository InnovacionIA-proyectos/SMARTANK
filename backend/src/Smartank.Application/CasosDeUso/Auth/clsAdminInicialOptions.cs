namespace Smartank.Application.CasosDeUso.Auth;

/// <summary>
/// Datos del administrador inicial (variables Admin__Email, Admin__Password, Admin__Nombre). Solo los lee el comando de
/// consola <c>crear-admin</c>; no se usan durante la operacion normal.
/// </summary>
public class clsAdminInicialOptions
{
    public const string SECCION = "Admin";

    public string? Email { get; set; }

    public string? Password { get; set; }

    public string? Nombre { get; set; }
}
