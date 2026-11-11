using Smartank.Application.Common.Abstractions;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Common;

/// <summary>
/// Unidad de trabajo sobre el DbContext de la peticion (scoped). Una conexion perdida se reintenta por
/// EnableRetryOnFailure; un conflicto de rowversion o de unicidad llega a Application como clsConflictException
/// (409 CONFLICTO_CONCURRENCIA / REGISTRO_DUPLICADO).
/// </summary>
public sealed class clsEfUnitOfWork(clsSmartankDbContext db) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default) => clsEfGuardado.GuardarAsync(db, ct);
}
