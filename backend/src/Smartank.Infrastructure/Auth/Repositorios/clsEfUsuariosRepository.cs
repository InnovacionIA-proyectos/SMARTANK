using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Auth.Repositorios;

public sealed class clsEfUsuariosRepository(clsSmartankDbContext db) : IUsuariosRepository
{
    // La columna Email usa collation insensible a mayusculas, asi que la comparacion directa basta.
    public Task<clsUsuario?> ObtenerPorEmailAsync(string emailNormalizado, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Email == emailNormalizado, ct);

    public Task<clsUsuario?> ObtenerPorIdAsync(Guid id, CancellationToken ct) =>
        db.Usuarios.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> ExisteEmailAsync(string emailNormalizado, CancellationToken ct) =>
        db.Usuarios.AnyAsync(u => u.Email == emailNormalizado, ct);

    public void Agregar(clsUsuario usuario) => db.Usuarios.Add(usuario);
}
