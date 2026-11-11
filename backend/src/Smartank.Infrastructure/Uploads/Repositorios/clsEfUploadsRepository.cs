using Microsoft.EntityFrameworkCore;
using Smartank.Application.CasosDeUso.Uploads.Interfaces;
using Smartank.Domain.Uploads.Entidades;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Uploads.Repositorios;

/// <summary>Imagenes como varbinary en tblUploads.</summary>
public sealed class clsEfUploadsRepository(clsSmartankDbContext db) : IUploadsRepository
{
    public void Agregar(clsUpload upload) => db.Uploads.Add(upload);

    public Task<clsUpload?> ObtenerAsync(Guid id, CancellationToken ct) =>
        db.Uploads.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
}
