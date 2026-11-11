using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Application.CasosDeUso.Auth.Interfaces;

public interface IUsuariosRepository
{
    /// <summary>Usuario por correo (ya normalizado a minusculas), o null si no existe. Sin seguimiento de cambios.</summary>
    Task<clsUsuario?> ObtenerPorEmailAsync(string emailNormalizado, CancellationToken ct);

    Task<clsUsuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct);

    Task<bool> ExisteEmailAsync(string emailNormalizado, CancellationToken ct);

    void Agregar(clsUsuario usuario);
}
