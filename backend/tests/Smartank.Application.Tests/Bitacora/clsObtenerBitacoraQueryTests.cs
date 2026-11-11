using Moq;
using Smartank.Application.CasosDeUso.Bitacora;
using Smartank.Application.CasosDeUso.Bitacora.DTOs;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Tests.Autorizaciones;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Bitacora;

public class clsObtenerBitacoraQueryTests
{
    private readonly Mock<IBitacoraTokenPort> _tokens = new();
    private readonly Mock<IBitacoraConsultas> _consultas = new();

    private clsObtenerBitacoraQueryHandler Handler() => new(_tokens.Object, _consultas.Object);

    [Fact]
    public async Task Con_un_token_valido_devuelve_la_orden()
    {
        var id = Guid.NewGuid();
        var dto = new clsBitacoraDto(id, "ABC123", "301457403", "Bogota - Medellin", "EDS Norte", 40m,
            EstadoAutorizacion.Autorizada, false, clsDatosPrueba.AHORA.AddHours(11));
        _tokens.Setup(t => t.Validar("tok")).Returns(id);
        _consultas.Setup(c => c.ObtenerAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

        var r = await Handler().Handle(new clsObtenerBitacoraQuery("tok"));

        Assert.Same(dto, r);
    }

    [Theory]
    [InlineData("BITACORA_TOKEN_INVALIDO")]
    [InlineData("BITACORA_TOKEN_EXPIRADO")]
    public async Task Un_token_invalido_o_expirado_se_propaga_como_401_sin_consultar_datos(string codigo)
    {
        _tokens.Setup(t => t.Validar(It.IsAny<string>())).Throws(new clsUnauthorizedException(codigo, "x"));

        var ex = await Assert.ThrowsAsync<clsUnauthorizedException>(() => Handler().Handle(new clsObtenerBitacoraQuery("tok")));

        Assert.Equal(codigo, ex.Code);
        _consultas.Verify(c => c.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_la_autorizacion_del_token_ya_no_existe_lanza_404()
    {
        _tokens.Setup(t => t.Validar("tok")).Returns(Guid.NewGuid());
        _consultas.Setup(c => c.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsBitacoraDto?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsObtenerBitacoraQuery("tok")));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }
}
