using System.Text;
using Smartank.Domain.Uploads.Reglas;

namespace Smartank.Domain.Tests.Uploads;

public class clsFirmaImagenTests
{
    private static byte[] Jpeg() => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];

    private static byte[] Webp() => [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WEBP"u8, .. "VP8 "u8];

    [Fact]
    public void Reconoce_jpeg()
    {
        Assert.Equal("image/jpeg", clsFirmaImagen.Detectar(Jpeg()));
    }

    [Fact]
    public void Reconoce_png()
    {
        Assert.Equal("image/png", clsFirmaImagen.Detectar(Png()));
    }

    [Fact]
    public void Reconoce_webp()
    {
        Assert.Equal("image/webp", clsFirmaImagen.Detectar(Webp()));
    }

    [Fact]
    public void Un_archivo_de_texto_no_es_imagen_aunque_diga_serlo()
    {
        Assert.Null(clsFirmaImagen.Detectar(Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>")));
    }

    [Fact]
    public void Un_ejecutable_no_es_imagen()
    {
        Assert.Null(clsFirmaImagen.Detectar([0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00]));
    }

    [Fact]
    public void Un_gif_no_esta_soportado()
    {
        Assert.Null(clsFirmaImagen.Detectar("GIF89a"u8.ToArray()));
    }

    [Fact]
    public void Un_riff_que_no_es_webp_no_es_imagen()
    {
        // Un WAV tambien empieza por RIFF; la marca WEBP va en los bytes 8-11.
        byte[] wav = [.. "RIFF"u8, 0x24, 0x00, 0x00, 0x00, .. "WAVE"u8, .. "fmt "u8];

        Assert.Null(clsFirmaImagen.Detectar(wav));
    }

    [Fact]
    public void Un_riff_truncado_no_es_imagen()
    {
        Assert.Null(clsFirmaImagen.Detectar("RIFF....WEB"u8.ToArray())); // 11 bytes
    }

    [Fact]
    public void Un_png_con_firma_incompleta_no_es_imagen()
    {
        Assert.Null(clsFirmaImagen.Detectar([0x89, 0x50, 0x4E, 0x47]));
    }

    [Fact]
    public void Un_jpeg_truncado_a_menos_de_3_bytes_no_es_imagen()
    {
        Assert.Null(clsFirmaImagen.Detectar([0xFF, 0xD8]));
    }

    [Fact]
    public void Un_arreglo_vacio_no_es_imagen()
    {
        Assert.Null(clsFirmaImagen.Detectar([]));
    }
}
