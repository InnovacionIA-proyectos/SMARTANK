using Smartank.Domain.Enums;

namespace Smartank.Domain.Usuarios.Entidades;

/// <summary>Cuenta de una persona que usa el sistema (tabla tblUsuarios). La autenticacion es propia contra la base.</summary>
public class clsUsuario
{
    public Guid Id { get; set; }

    /// <summary>Email unico (login). La base lo compara sin distinguir mayusculas.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Hash bcrypt de la contrasena. Dato sensible: nunca se serializa al API.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public string? Nombre { get; set; }

    public Rol Rol { get; set; } = Rol.Despacho;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
