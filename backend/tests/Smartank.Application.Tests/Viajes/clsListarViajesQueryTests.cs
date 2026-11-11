using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Viajes;
using Smartank.Application.CasosDeUso.Viajes.DTOs;
using Smartank.Application.CasosDeUso.Viajes.Interfaces;
using Smartank.Application.Common.Configuracion;
using Smartank.Domain.Enums;
using Smartank.Domain.Viajes.Reglas;

namespace Smartank.Application.Tests.Viajes;

public class clsListarViajesQueryTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<IViajesLecturaRepository> _viajes = new();

    public clsListarViajesQueryTests()
    {
        _viajes.Setup(v => v.ObtenerOpcionesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsViajesOpcionesDto(["Bogota-Villavicencio"], [2026, 2025]));
    }

    private clsListarViajesQueryHandler Handler(double rendimientoDefault = 8) => new(
        _viajes.Object,
        Options.Create(new clsParametrosNegocio { RendimientoDefaultKmGal = rendimientoDefault }),
        new RelojFijo(AHORA));

    private static clsListarViajesQuery Consulta(
        string? estado = null, string? ruta = null, string? placa = null, string? manifiesto = null, string? anio = null,
        string? mes = null, string? desplazamiento = null, string? conRecomendacion = null, string? telemetria = null) =>
        new(estado, ruta, placa, manifiesto, anio, mes, desplazamiento, conRecomendacion, telemetria);

    private static clsViajesFiltro Filtro(clsListarViajesQuery q) => clsListarViajesQueryHandler.Normalizar(q, AHORA);

    private static clsViajeListadoFila Fila(
        decimal? nivel = 40m, double? rendHistorico = 8.5, double? rendEsperado = null, double tolerancia = 0.02,
        double? rutaKm = 666, bool recomendacion = false) => new(
        Guid.Parse("11111111-aaaa-aaaa-aaaa-111111111111"), Guid.Parse("22222222-bbbb-bbbb-bbbb-222222222222"),
        "Bogota-Villavicencio", "BOGOTA - VILLAVICENCIO", "BOG-VVC", EstadoViaje.EnRuta, 330, "1234567890",
        new DateTimeOffset(2026, 6, 14, 6, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 6, 14, 5, 0, 0, TimeSpan.Zero),
        "SNU123", "diesel", nivel, 150m, AHORA.AddMinutes(-5), AHORA.AddMinutes(-10), 4.6, -74.1,
        rendHistorico, rendEsperado, tolerancia, rutaKm, 4.6, -74.1, 4.15, -73.6, recomendacion);

    // --- Normalizacion de filtros: un valor desconocido se ignora, no es un error (como el origen) ---

    [Theory]
    [InlineData("programado", EstadoViaje.Programado)]
    [InlineData("en_ruta", EstadoViaje.EnRuta)]
    [InlineData("descartado", EstadoViaje.Descartado)]
    public void Estado_valido_se_aplica(string texto, EstadoViaje esperado)
    {
        Assert.Equal(esperado, Filtro(Consulta(estado: texto)).Estado);
    }

    [Theory]
    [InlineData("cerrado")]
    [InlineData("EnRuta")]
    [InlineData("EN_RUTA")]
    [InlineData("volando")]
    [InlineData("")]
    [InlineData(null)]
    public void Estado_no_reconocido_o_cerrado_se_ignora(string? texto)
    {
        Assert.Null(Filtro(Consulta(estado: texto)).Estado);
    }

    [Fact]
    public void Texto_se_recorta_y_el_vacio_equivale_a_sin_filtro()
    {
        var f = Filtro(Consulta(ruta: "  Bogota-Villavicencio ", placa: " snu ", manifiesto: "  "));

        Assert.Equal("Bogota-Villavicencio", f.Ruta);
        Assert.Equal("snu", f.Placa);
        Assert.Null(f.Manifiesto);
    }

    [Theory]
    [InlineData("2026", 2026)]
    [InlineData(" 2026 ", 2026)]
    [InlineData("12", 12)]
    [InlineData("abc", null)]
    [InlineData("2026.5", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Anio_y_mes_solo_aceptan_enteros(string? texto, int? esperado)
    {
        var f = Filtro(Consulta(anio: texto, mes: texto));

        Assert.Equal(esperado, f.Anio);
        Assert.Equal(esperado, f.Mes);
    }

    [Theory]
    [InlineData("vacio", TipoDesplazamiento.Vacio)]
    [InlineData("cargado", TipoDesplazamiento.Cargado)]
    [InlineData("otro", null)]
    [InlineData("VACIO", null)]
    [InlineData(null, null)]
    public void Desplazamiento_solo_acepta_vacio_o_cargado(string? texto, TipoDesplazamiento? esperado)
    {
        Assert.Equal(esperado, Filtro(Consulta(desplazamiento: texto)).Desplazamiento);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("true", false)]
    [InlineData(null, false)]
    public void Con_recomendacion_solo_se_activa_con_el_texto_1(string? texto, bool esperado)
    {
        Assert.Equal(esperado, Filtro(Consulta(conRecomendacion: texto)).SoloRecomendacionActiva);
    }

    [Theory]
    [InlineData("si", true)]
    [InlineData("no", false)]
    [InlineData("quizas", null)]
    [InlineData(null, null)]
    public void Telemetria_reciente_distingue_si_no_y_todos(string? texto, bool? esperado)
    {
        Assert.Equal(esperado, Filtro(Consulta(telemetria: texto)).TelemetriaReciente);
    }

    [Fact]
    public void La_telemetria_reciente_cuenta_la_ultima_hora()
    {
        Assert.Equal(AHORA.AddHours(-1), Filtro(Consulta()).UmbralTelemetria);
    }

    // --- Handler ---

    [Fact]
    public async Task Pasa_el_filtro_normalizado_al_repositorio()
    {
        _viajes.Setup(v => v.ListarAsync(It.IsAny<clsViajesFiltro>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await Handler().Handle(Consulta(estado: "en_ruta", ruta: "Ruta X", anio: "2026", desplazamiento: "vacio", conRecomendacion: "1", telemetria: "si"));

        _viajes.Verify(v => v.ListarAsync(
            It.Is<clsViajesFiltro>(f =>
                f.Estado == EstadoViaje.EnRuta && f.Ruta == "Ruta X" && f.Anio == 2026 && f.Mes == null &&
                f.Desplazamiento == TipoDesplazamiento.Vacio && f.SoloRecomendacionActiva && f.TelemetriaReciente == true &&
                f.UmbralTelemetria == AHORA.AddHours(-1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Mapea_la_fila_y_calcula_la_autonomia_actual_del_vehiculo()
    {
        _viajes.Setup(v => v.ListarAsync(It.IsAny<clsViajesFiltro>(), It.IsAny<CancellationToken>())).ReturnsAsync([Fila(recomendacion: true)]);

        var respuesta = await Handler().Handle(Consulta());

        Assert.Equal(1, respuesta.Total);
        var dto = Assert.Single(respuesta.Viajes);
        Assert.Equal("SNU123", dto.Placa);
        Assert.Equal("BOGOTA - VILLAVICENCIO", dto.NombreRuta);
        Assert.Equal(EstadoViaje.EnRuta, dto.Estado);
        Assert.Equal(330, dto.AutonomiaEstimadaKm);
        // 40 gal x 8.5 km/gal x (1 - 0.02) = 333.2 -> 333
        Assert.Equal(333, dto.AutonomiaKm);
        Assert.Equal(666, dto.RutaKm);
        Assert.Equal(4.6, dto.VehiculoLat);
        Assert.Equal(4.15, dto.DestinoLat);
        Assert.True(dto.RecomendacionActiva);
        Assert.Equal(["Bogota-Villavicencio"], respuesta.Opciones.Rutas);
        Assert.Equal([2026, 2025], respuesta.Opciones.Anios);
    }

    [Fact]
    public async Task Sin_rendimiento_propio_la_autonomia_usa_el_default_de_negocio_configurado()
    {
        _viajes.Setup(v => v.ListarAsync(It.IsAny<clsViajesFiltro>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([Fila(rendHistorico: null, rendEsperado: null)]);

        // 40 x 10 x 0.98 = 392
        var respuesta = await Handler(rendimientoDefault: 10).Handle(Consulta());

        Assert.Equal(392, respuesta.Viajes[0].AutonomiaKm);
    }

    [Fact]
    public async Task Vehiculo_sin_lectura_de_nivel_no_tiene_autonomia()
    {
        _viajes.Setup(v => v.ListarAsync(It.IsAny<clsViajesFiltro>(), It.IsAny<CancellationToken>())).ReturnsAsync([Fila(nivel: null)]);

        var respuesta = await Handler().Handle(Consulta());

        Assert.Null(respuesta.Viajes[0].AutonomiaKm);
        Assert.Null(respuesta.Viajes[0].NivelTanqueActual);
    }

    [Fact]
    public async Task Sin_viajes_devuelve_total_cero_y_las_opciones_igual()
    {
        _viajes.Setup(v => v.ListarAsync(It.IsAny<clsViajesFiltro>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var respuesta = await Handler().Handle(Consulta(ruta: "ruta-que-no-existe"));

        Assert.Equal(0, respuesta.Total);
        Assert.Empty(respuesta.Viajes);
        Assert.NotEmpty(respuesta.Opciones.Rutas);
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
