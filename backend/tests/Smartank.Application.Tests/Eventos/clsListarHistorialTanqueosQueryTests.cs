using Moq;
using Smartank.Application.CasosDeUso.Eventos;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Application.Common.Exceptions;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Eventos;

public class clsListarHistorialTanqueosQueryTests
{
    private readonly Mock<IEventosConsultas> _consultas = new();

    private clsListarHistorialTanqueosQueryHandler Handler() => new(_consultas.Object);

    private static clsHistorialTanqueoFila Fila(
        string? estacion = "EDS Terpel Norte", string? ciudad = "Bogota", decimal? galones = 40m, string? tipoMotor = "diesel",
        bool autorizado = false, FuenteTanqueo fuente = FuenteTanqueo.Terpel) => new(
        Guid.NewGuid(), fuente, "SNU123", tipoMotor, "TM_MED-CTG SYT", estacion, ciudad,
        new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero), galones, 9800m, 123456, autorizado,
        autorizado ? 9500m : null, null, null, null);

    private clsFiltroHistorial? _filtro;

    private void Devuelve(params clsHistorialTanqueoFila[] filas) =>
        _consultas.Setup(c => c.ListarHistorialAsync(It.IsAny<clsFiltroHistorial>(), It.IsAny<CancellationToken>()))
            .Callback<clsFiltroHistorial, CancellationToken>((f, _) => _filtro = f)
            .ReturnsAsync(filas);

    [Fact]
    public async Task Mapea_las_filas_con_el_total_y_el_enlace_con_la_autorizacion()
    {
        Devuelve(Fila(), Fila(autorizado: true, fuente: FuenteTanqueo.Distracom));

        var r = await Handler().Handle(new clsListarHistorialTanqueosQuery(null, null, null, null));

        Assert.Equal(2, r.Total);
        Assert.Equal(FuenteTanqueo.Terpel, r.Eventos[0].Proveedor);
        Assert.False(r.Eventos[0].Autorizado);
        Assert.Null(r.Eventos[0].PrecioTecho);
        Assert.Equal(FuenteTanqueo.Distracom, r.Eventos[1].Proveedor);
        Assert.True(r.Eventos[1].Autorizado);
        Assert.Equal(9500m, r.Eventos[1].PrecioTecho);
        Assert.Equal("TM_MED-CTG SYT", r.Eventos[0].Programa);
        Assert.Equal("SNU123", r.Eventos[0].Placa);
    }

    [Fact]
    public async Task La_ruta_de_la_estacion_es_la_ciudad_porque_el_historial_real_no_trae_ruta()
    {
        Devuelve(Fila(ciudad: "Medellin"));

        var r = await Handler().Handle(new clsListarHistorialTanqueosQuery(null, null, null, null));

        Assert.Equal("Medellin", r.Eventos[0].EstacionRuta);
    }

    [Fact]
    public async Task Sin_nombre_de_estacion_usa_la_ciudad_y_sin_ninguna_un_guion_largo()
    {
        Devuelve(Fila(estacion: "  ", ciudad: "Cali"), Fila(estacion: null, ciudad: null));

        var r = await Handler().Handle(new clsListarHistorialTanqueosQuery(null, null, null, null));

        Assert.Equal("Cali", r.Eventos[0].EstacionNombre);
        Assert.Equal("—", r.Eventos[1].EstacionNombre);
    }

    [Fact]
    public async Task Galones_y_tipo_de_motor_nulos_salen_como_cero_y_texto_vacio()
    {
        Devuelve(Fila(galones: null, tipoMotor: null));

        var r = await Handler().Handle(new clsListarHistorialTanqueosQuery(null, null, null, null));

        Assert.Equal(0m, r.Eventos[0].GalonesCargados);
        Assert.Equal(string.Empty, r.Eventos[0].TipoMotor);
    }

    [Fact]
    public async Task Pasa_los_filtros_normalizados_a_la_consulta()
    {
        Devuelve();

        await Handler().Handle(new clsListarHistorialTanqueosQuery(" snu ", "terpel", "2026-06-01", "2026-06-15"));

        Assert.Equal(
            new clsFiltroHistorial("snu", FuenteTanqueo.Terpel, new DateOnly(2026, 6, 1), new DateOnly(2026, 6, 15)), _filtro);
    }

    [Fact]
    public async Task Los_filtros_vacios_o_en_blanco_se_ignoran()
    {
        Devuelve();

        await Handler().Handle(new clsListarHistorialTanqueosQuery("  ", "", " ", null));

        Assert.Equal(new clsFiltroHistorial(null, null, null, null), _filtro);
    }

    [Theory]
    [InlineData("shell")]
    [InlineData("Terpel")]
    public async Task Un_proveedor_invalido_lanza_400(string proveedor)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsListarHistorialTanqueosQuery(null, proveedor, null, null)));

        Assert.Equal("PROVEEDOR_INVALIDO", ex.Code);
    }

    [Theory]
    [InlineData("01-06-2026")]
    [InlineData("2026-6-1")]
    [InlineData("2026-13-01")]
    [InlineData("2026-02-30")]
    [InlineData("ayer")]
    public async Task Una_fecha_invalida_lanza_400_en_desde_o_hasta(string fecha)
    {
        var exDesde = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsListarHistorialTanqueosQuery(null, null, fecha, null)));
        var exHasta = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsListarHistorialTanqueosQuery(null, null, null, fecha)));

        Assert.Equal("FECHA_INVALIDA", exDesde.Code);
        Assert.Equal("FECHA_INVALIDA", exHasta.Code);
    }
}
