namespace Smartank.Application.Common.Abstractions;

/// <summary>Application decide CUANDO confirmar los cambios; Infrastructure decide COMO.</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
