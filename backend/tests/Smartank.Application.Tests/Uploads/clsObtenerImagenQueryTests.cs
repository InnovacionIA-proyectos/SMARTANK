using Moq;
using Smartank.Application.CasosDeUso.Uploads;
using Smartank.Application.CasosDeUso.Uploads.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Uploads.Entidades;

namespace Smartank.Application.Tests.Uploads;

public class clsObtenerImagenQueryTests
{
    private readonly Mock<IUploadsRepository> _uploads = new();

    [Fact]
    public async Task Devuelve_el_tipo_y_los_bytes_guardados()
    {
        var upload = new clsUpload { Id = Guid.NewGuid(), ContentType = "image/png", Data = [1, 2, 3] };
        _uploads.Setup(u => u.ObtenerAsync(upload.Id, It.IsAny<CancellationToken>())).ReturnsAsync(upload);

        var r = await new clsObtenerImagenQueryHandler(_uploads.Object).Handle(new clsObtenerImagenQuery(upload.Id));

        Assert.Equal("image/png", r.ContentType);
        Assert.Equal(new byte[] { 1, 2, 3 }, r.Datos);
    }

    [Fact]
    public async Task Imagen_inexistente_lanza_404()
    {
        _uploads.Setup(u => u.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsUpload?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(
            () => new clsObtenerImagenQueryHandler(_uploads.Object).Handle(new clsObtenerImagenQuery(Guid.NewGuid())));

        Assert.Equal("IMAGEN_NO_ENCONTRADA", ex.Code);
    }
}
