namespace Smartank.Domain.Uploads.Entidades;

/// <summary>Imagen subida (foto del surtidor de la bitacora) guardada como bytes en la base (tabla tblUploads). Inmutable.</summary>
public class clsUpload
{
    public Guid Id { get; set; }

    public string ContentType { get; set; } = string.Empty;

    public byte[] Data { get; set; } = [];

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
