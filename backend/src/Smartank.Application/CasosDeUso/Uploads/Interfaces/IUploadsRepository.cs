using Smartank.Domain.Uploads.Entidades;

namespace Smartank.Application.CasosDeUso.Uploads.Interfaces;

/// <summary>Almacenamiento de las imagenes subidas (bytes en la base). Las imagenes son inmutables: solo se agregan y se leen.</summary>
public interface IUploadsRepository
{
    /// <summary>Registra la imagen (se confirma con <c>IUnitOfWork</c>).</summary>
    void Agregar(clsUpload upload);

    /// <summary>Imagen por id, sin seguimiento; null si no existe.</summary>
    Task<clsUpload?> ObtenerAsync(Guid id, CancellationToken ct);
}
