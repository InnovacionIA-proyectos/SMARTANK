namespace Smartank.Domain.Uploads.Reglas;

/// <summary>
/// Reconoce el formato REAL de una imagen por su firma (los primeros bytes). El Content-Type que declara el cliente no es
/// confiable: un archivo cualquiera puede decir "image/jpeg". Solo se aceptan JPEG, PNG y WEBP.
/// </summary>
public static class clsFirmaImagen
{
    private static readonly byte[] JPEG = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PNG = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] RIFF = [0x52, 0x49, 0x46, 0x46]; // "RIFF"
    private static readonly byte[] WEBP = [0x57, 0x45, 0x42, 0x50]; // "WEBP"

    /// <summary>"image/jpeg", "image/png" o "image/webp" segun la firma; null si no es una imagen soportada.</summary>
    public static string? Detectar(ReadOnlySpan<byte> datos)
    {
        if (datos.StartsWith(JPEG)) return "image/jpeg";
        if (datos.StartsWith(PNG)) return "image/png";
        // WEBP: "RIFF" + tamano (4 bytes) + "WEBP".
        if (datos.Length >= 12 && datos[..4].SequenceEqual(RIFF) && datos.Slice(8, 4).SequenceEqual(WEBP))
            return "image/webp";
        return null;
    }
}
