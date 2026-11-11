using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Autorizaciones;

public class clsAutorizacionesQueriesTests
{
    private readonly Mock<IAutorizacionesConsultas> _consultas = new();
    private readonly clsParametrosNegocio _parametros = new() { RendimientoDefaultKmGal = 7.5 };

    private static clsAutorizacionDetalleDto Detalle() => new() { Id = Guid.NewGuid(), Placa = "ABC123", EstacionNombre = "EDS" };

    [Fact]
    public async Task Detalle_devuelve_la_autorizacion_con_el_rendimiento_default_de_negocio()
    {
        var detalle = Detalle();
        _consultas.Setup(c => c.ObtenerDetalleAsync(detalle.Id, 7.5, It.IsAny<CancellationToken>())).ReturnsAsync(detalle);

        var r = await new clsObtenerAutorizacionDetalleQueryHandler(_consultas.Object, Options.Create(_parametros))
            .Handle(new clsObtenerAutorizacionDetalleQuery(detalle.Id));

        Assert.Same(detalle, r);
    }

    [Fact]
    public async Task Detalle_inexistente_lanza_404()
    {
        _consultas.Setup(c => c.ObtenerDetalleAsync(It.IsAny<Guid>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsAutorizacionDetalleDto?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(
            () => new clsObtenerAutorizacionDetalleQueryHandler(_consultas.Object, Options.Create(_parametros))
                .Handle(new clsObtenerAutorizacionDetalleQuery(Guid.NewGuid())));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }

    private clsListarAutorizacionesQueryHandler ListarHandler() => new(_consultas.Object, Options.Create(_parametros));

    [Fact]
    public async Task Listar_sin_filtros_pasa_el_limite_de_200_y_cuenta_el_total()
    {
        clsFiltroAutorizaciones? filtro = null;
        _consultas.Setup(c => c.ListarDetallesAsync(It.IsAny<clsFiltroAutorizaciones>(), 7.5, It.IsAny<CancellationToken>()))
            .Callback<clsFiltroAutorizaciones, double, CancellationToken>((f, _, _) => filtro = f)
            .ReturnsAsync([Detalle(), Detalle()]);

        var r = await ListarHandler().Handle(new clsListarAutorizacionesQuery(null, null));

        Assert.Equal(2, r.Total);
        Assert.Equal(2, r.Autorizaciones.Count);
        Assert.Equal(new clsFiltroAutorizaciones(null, null, 200), filtro);
    }

    [Fact]
    public async Task Listar_traduce_el_estado_snake_case_y_el_viaje_al_filtro()
    {
        var viajeId = Guid.NewGuid();
        clsFiltroAutorizaciones? filtro = null;
        _consultas.Setup(c => c.ListarDetallesAsync(It.IsAny<clsFiltroAutorizaciones>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .Callback<clsFiltroAutorizaciones, double, CancellationToken>((f, _, _) => filtro = f)
            .ReturnsAsync([]);

        await ListarHandler().Handle(new clsListarAutorizacionesQuery(" ejecutada_parcial ", viajeId));

        Assert.Equal(new clsFiltroAutorizaciones(EstadoAutorizacion.EjecutadaParcial, viajeId, 200), filtro);
    }

    [Theory]
    [InlineData("EjecutadaParcial")]
    [InlineData("desconocido")]
    public async Task Listar_con_estado_invalido_lanza_400(string estado)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => ListarHandler().Handle(new clsListarAutorizacionesQuery(estado, null)));

        Assert.Equal("ESTADO_INVALIDO", ex.Code);
    }
}
