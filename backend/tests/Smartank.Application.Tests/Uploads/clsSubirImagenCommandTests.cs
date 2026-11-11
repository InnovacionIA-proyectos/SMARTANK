using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.CasosDeUso.Uploads;
using Smartank.Application.CasosDeUso.Uploads.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Tests.Autorizaciones;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Uploads.Entidades;

namespace Smartank.Application.Tests.Uploads;

public class clsSubirImagenCommandTests
{
    private const string URL_SOLICITUD = "http://localhost:5000";

    private readonly Mock<IBitacoraTokenPort> _tokens = new();
    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IUploadsRepository> _uploads = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly clsAplicacionOptions _app = new() { UrlPublica = "https://smartank.test" };
    private readonly clsAutorizacionTanqueo _autorizacion = clsDatosPrueba.Autorizacion(EstadoAutorizacion.Autorizada);
    private clsUpload? _agregado;

    private static byte[] Jpeg(int extra = 20) => [0xFF, 0xD8, 0xFF, 0xE0, .. new byte[extra]];

    private static byte[] Png() => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    public clsSubirImagenCommandTests()
    {
        _tokens.Setup(t => t.Validar("tok")).Returns(_autorizacion.Id);
        _autorizaciones.Setup(a => a.ObtenerAsync(_autorizacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_autorizacion);
        _uploads.Setup(u => u.Agregar(It.IsAny<clsUpload>())).Callback<clsUpload>(u => { u.Id = Guid.NewGuid(); _agregado = u; });
    }

    private clsSubirImagenCommandHandler Handler() =>
        new(_tokens.Object, _autorizaciones.Object, _uploads.Object, _uow.Object, Options.Create(_app));

    [Fact]
    public async Task Guarda_la_imagen_y_devuelve_id_y_url_con_la_url_publica()
    {
        var r = await Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", Jpeg(), URL_SOLICITUD));

        Assert.Equal(_agregado!.Id, r.Id);
        Assert.Equal($"https://smartank.test/api/uploads/{r.Id}", r.Url);
        Assert.Equal("image/jpeg", _agregado.ContentType);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_url_publica_usa_el_origen_de_la_peticion()
    {
        _app.UrlPublica = null;

        var r = await Handler().Handle(new clsSubirImagenCommand("tok", "image/png", Png(), URL_SOLICITUD + "/"));

        Assert.Equal($"{URL_SOLICITUD}/api/uploads/{r.Id}", r.Url);
    }

    [Fact]
    public async Task El_tipo_guardado_es_el_real_por_la_firma_no_el_declarado()
    {
        // Un PNG verdadero declarado como jpeg: se guarda y sirve como png.
        await Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", Png(), URL_SOLICITUD));

        Assert.Equal("image/png", _agregado!.ContentType);
    }

    [Fact]
    public async Task El_content_type_en_mayusculas_y_con_parametros_se_acepta_si_empieza_por_image()
    {
        var r = await Handler().Handle(new clsSubirImagenCommand("tok", "IMAGE/JPEG; charset=binary", Jpeg(), URL_SOLICITUD));

        Assert.NotEqual(Guid.Empty, r.Id);
    }

    // ---- Content-Type mentiroso -----------------------------------------------------------------------------------

    [Fact]
    public async Task Un_archivo_de_texto_con_content_type_de_imagen_se_rechaza_por_su_firma()
    {
        var html = System.Text.Encoding.UTF8.GetBytes("<html><script>alert(1)</script></html>");

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", html, URL_SOLICITUD)));

        Assert.Equal("IMAGEN_INVALIDA", ex.Code);
        _uploads.Verify(u => u.Agregar(It.IsAny<clsUpload>()), Times.Never);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("application/octet-stream")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Un_content_type_que_no_es_imagen_se_rechaza_aunque_los_bytes_sean_una_imagen(string? contentType)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsSubirImagenCommand("tok", contentType, Jpeg(), URL_SOLICITUD)));

        Assert.Equal("ARCHIVO_NO_ES_IMAGEN", ex.Code);
    }

    // ---- Tamano -----------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Un_archivo_vacio_lanza_400()
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", [], URL_SOLICITUD)));

        Assert.Equal("ARCHIVO_VACIO", ex.Code);
    }

    [Fact]
    public async Task Exactamente_5_MB_se_acepta()
    {
        var datos = Jpeg(clsSubirImagenCommandHandler.MAX_BYTES - 4);
        Assert.Equal(5 * 1024 * 1024, datos.Length);

        var r = await Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", datos, URL_SOLICITUD));

        Assert.NotEqual(Guid.Empty, r.Id);
    }

    [Fact]
    public async Task Un_byte_sobre_5_MB_lanza_400()
    {
        var datos = Jpeg(clsSubirImagenCommandHandler.MAX_BYTES - 3);
        Assert.Equal(5 * 1024 * 1024 + 1, datos.Length);

        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", datos, URL_SOLICITUD)));

        Assert.Equal("ARCHIVO_DEMASIADO_GRANDE", ex.Code);
    }

    // ---- Token y estado ---------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("BITACORA_TOKEN_INVALIDO")]
    [InlineData("BITACORA_TOKEN_EXPIRADO")]
    public async Task Un_token_invalido_lanza_401_antes_de_mirar_el_archivo(string codigo)
    {
        _tokens.Setup(t => t.Validar(It.IsAny<string>())).Throws(new clsUnauthorizedException(codigo, "x"));

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(
            () => Handler().Handle(new clsSubirImagenCommand("malo", "text/html", [], URL_SOLICITUD)));

        Assert.Equal(codigo, ex.Code);
        _uploads.Verify(u => u.Agregar(It.IsAny<clsUpload>()), Times.Never);
    }

    [Fact]
    public async Task Un_token_ausente_se_trata_como_invalido()
    {
        _tokens.Setup(t => t.Validar(string.Empty)).Throws(new clsUnauthorizedException("BITACORA_TOKEN_INVALIDO", "x"));

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(
            () => Handler().Handle(new clsSubirImagenCommand(null, "image/jpeg", Jpeg(), URL_SOLICITUD)));

        Assert.Equal("BITACORA_TOKEN_INVALIDO", ex.Code);
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.Expirada)]
    [InlineData(EstadoAutorizacion.Rechazada)]
    public async Task La_autorizacion_del_token_debe_estar_autorizada(EstadoAutorizacion estado)
    {
        _autorizacion.Estado = estado;

        var ex = await Assert.ThrowsAsync<clsConflictException>(
            () => Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", Jpeg(), URL_SOLICITUD)));

        Assert.Equal("AUTORIZACION_NO_AUTORIZADA", ex.Code);
        _uploads.Verify(u => u.Agregar(It.IsAny<clsUpload>()), Times.Never);
    }

    [Fact]
    public async Task Autorizacion_inexistente_lanza_404()
    {
        _autorizaciones.Setup(a => a.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAutorizacionTanqueo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(
            () => Handler().Handle(new clsSubirImagenCommand("tok", "image/jpeg", Jpeg(), URL_SOLICITUD)));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }
}
