using Moq;
using Smartank.Application.CasosDeUso.Metricas;
using Smartank.Application.CasosDeUso.Metricas.DTOs;
using Smartank.Application.CasosDeUso.Metricas.Interfaces;
using Smartank.Domain.Metricas.Reglas;

namespace Smartank.Application.Tests.Metricas;

public class clsObtenerMetricasRecuperacionQueryTests
{
    private static readonly TimeSpan BOGOTA = TimeSpan.FromHours(-5);
    private static readonly clsOpcionesFiltroDto OPCIONES = new(
        ["MM_AUTECO SYT", "TM_MED-CTG SYT"], ["terpel", "distracom"], ["diesel"], [2026], [6], ["bogota-villavicencio"]);

    private readonly Mock<IMetricasRepository> _metricas = new();

    public clsObtenerMetricasRecuperacionQueryTests()
    {
        _metricas.Setup(m => m.ObtenerOpcionesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(OPCIONES);
    }

    private clsObtenerMetricasRecuperacionQueryHandler Handler() => new(_metricas.Object);

    private static clsObtenerMetricasRecuperacionQuery Consulta(
        string? programa = null, string? proveedor = null, string? producto = null, string? anio = null, string? mes = null, string? ruta = null) =>
        new(programa, proveedor, producto, anio, mes, ruta);

    private static clsTanqueoMetrica Tanqueo(
        string placa, int dia, decimal galones, decimal precio, decimal? min, decimal? max, string proveedor = "terpel") =>
        new(Guid.NewGuid(), proveedor, placa, new DateTimeOffset(2026, 6, dia, 10, 0, 0, BOGOTA), galones, precio, galones * precio,
            "EDS Primax Centro", "Bogotá", "bogota-villavicencio", min, max, null, null);

    [Fact]
    public async Task Calcula_ahorro_y_oportunidad_con_datos_hechos_a_mano()
    {
        // Real = 100x9400 = 940.000. Ideal = 100x9300 = 930.000. Maximo = 100x10100 = 1.010.000 (mismo caso que el test del origen).
        _metricas.Setup(m => m.ListarTanqueosAsync(It.IsAny<clsRecuperacionFiltrosDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([
                Tanqueo("SNU123", 10, 40m, 9400m, 9300m, 10100m),
                Tanqueo("SNU123", 12, 60m, 9400m, 9300m, 10100m, "distracom"),
            ]);

        var dto = await Handler().Handle(Consulta());

        Assert.Equal(100m, dto.GalonesTotal);
        Assert.Equal(2, dto.TanqueosTotal);
        Assert.Equal(940_000m, dto.GastoReal);
        Assert.Equal(930_000m, dto.GastoIdeal);
        Assert.Equal(1_010_000m, dto.GastoMaximo);
        Assert.Equal(70_000m, dto.AhorroVsMaximo);
        Assert.Equal(10_000m, dto.OportunidadVsIdeal);
        Assert.Equal(9300m, dto.PrecioMinimoRuta);
        Assert.Equal(2, dto.UtilizacionEds.Count);
        Assert.Equal("SNU123", Assert.Single(dto.Equipos).Equipo);
    }

    [Fact]
    public async Task Sin_tanqueos_todo_en_cero_sin_division_por_cero_y_con_las_opciones()
    {
        _metricas.Setup(m => m.ListarTanqueosAsync(It.IsAny<clsRecuperacionFiltrosDto>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var dto = await Handler().Handle(Consulta());

        Assert.Equal(0, dto.TanqueosTotal);
        Assert.Equal(0m, dto.GalonesTotal);
        Assert.Equal(0m, dto.AhorroVsMaximo);
        Assert.Equal(0m, dto.OportunidadVsIdeal);
        Assert.Null(dto.PrecioMinimoRuta);
        Assert.Empty(dto.UtilizacionEds);
        Assert.Empty(dto.Equipos);
        Assert.Empty(dto.GalonesPorEds);
        Assert.Empty(dto.Rutas);
        Assert.Equal(["MM_AUTECO SYT", "TM_MED-CTG SYT"], dto.Opciones.Programas);
        Assert.Equal(["terpel", "distracom"], dto.Opciones.Proveedores);
    }

    [Fact]
    public async Task Los_filtros_se_normalizan_se_pasan_al_repositorio_y_se_devuelven()
    {
        _metricas.Setup(m => m.ListarTanqueosAsync(It.IsAny<clsRecuperacionFiltrosDto>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var dto = await Handler().Handle(Consulta(" TM_MED-CTG SYT ", "terpel", "diesel", "2026", "6", "bogota-villavicencio"));

        var esperado = new clsRecuperacionFiltrosDto("TM_MED-CTG SYT", "terpel", "diesel", 2026, 6, "bogota-villavicencio");
        Assert.Equal(esperado, dto.Filtros);
        _metricas.Verify(m => m.ListarTanqueosAsync(esperado, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("2026", 2026)]
    [InlineData("2026.9", 2026)]    // como el origen: se trunca
    [InlineData("-3", -3)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("NaN", null)]
    [InlineData("1e30", null)]      // fuera del rango de int
    public void Anio_y_mes_se_truncan_a_entero_y_lo_no_numerico_se_ignora(string? texto, int? esperado)
    {
        var f = clsObtenerMetricasRecuperacionQueryHandler.Normalizar(Consulta(anio: texto, mes: texto));

        Assert.Equal(esperado, f.Anio);
        Assert.Equal(esperado, f.Mes);
    }

    [Fact]
    public void Filtros_de_texto_en_blanco_equivalen_a_sin_filtro()
    {
        var f = clsObtenerMetricasRecuperacionQueryHandler.Normalizar(Consulta(programa: "  ", proveedor: "", producto: null, ruta: " "));

        Assert.Equal(new clsRecuperacionFiltrosDto(null, null, null, null, null, null), f);
    }
}
