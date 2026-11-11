using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Enums;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsSincronizarConductoresYTanqueosTests
{
    // ---------- Conductores ----------

    private readonly Mock<IConductoresLegadoPort> _conductoresLegado = new();
    private readonly Mock<IConductoresSincronizacionRepository> _conductoresRepo = new();
    private IReadOnlyList<clsConductorUpsertDto> _conductores = [];

    private clsSincronizarConductoresCommandHandler HandlerConductores(
        IEnumerable<clsConductorLegadoDto> lista, Dictionary<string, string> celulares)
    {
        _conductoresLegado.Setup(l => l.ListarAsync(It.IsAny<CancellationToken>())).ReturnsAsync(lista.ToList());
        _conductoresLegado.Setup(l => l.ListarCelularesAplMovilAsync(It.IsAny<CancellationToken>())).ReturnsAsync(celulares);
        _conductoresRepo.Setup(r => r.UpsertAsync(It.IsAny<IReadOnlyList<clsConductorUpsertDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsConductorUpsertDto>, CancellationToken>((l, _) => _conductores = l)
            .ReturnsAsync((IReadOnlyList<clsConductorUpsertDto> l, CancellationToken _) => new clsResultadoUpsertDto(l.Count, 0, []));
        return new(_conductoresLegado.Object, _conductoresRepo.Object, NullLogger<clsSincronizarConductoresCommandHandler>.Instance);
    }

    private static clsConductorLegadoDto Cond(long id, string cedula, string nombres = " Juan Perez ") =>
        new(id, cedula, nombres, true, false);

    [Fact]
    public async Task El_celular_de_la_app_movil_se_cruza_por_cedula_y_se_normaliza_a_E164()
    {
        var h = HandlerConductores([Cond(1, "123")], new() { ["123"] = "304 675 8799" });

        var r = await h.Handle(new clsSincronizarConductoresCommand());

        var c = _conductores.Single();
        Assert.Equal("+573046758799", c.NumeroCelular);
        Assert.Equal("Juan Perez", c.Nombres);
        Assert.Equal(1, r.Extras["conCelular"]);
    }

    [Theory]
    [InlineData("12345678")]        // una cedula escrita en el campo de celular
    [InlineData("no tiene")]
    [InlineData("")]
    public async Task Si_el_celular_no_es_valido_queda_null_y_no_se_inventa_uno(string crudo)
    {
        var h = HandlerConductores([Cond(1, "123")], new() { ["123"] = crudo });

        var r = await h.Handle(new clsSincronizarConductoresCommand());

        Assert.Null(_conductores.Single().NumeroCelular);
        Assert.Equal(0, r.Extras["conCelular"]);
    }

    [Fact]
    public async Task Un_conductor_que_no_esta_en_la_app_movil_queda_sin_celular()
    {
        var h = HandlerConductores([Cond(1, "123")], []);

        await h.Handle(new clsSincronizarConductoresCommand());

        Assert.Null(_conductores.Single().NumeroCelular);
    }

    [Fact]
    public async Task Omite_las_cedulas_vacias_y_deja_una_sola_fila_por_cedula_repetida()
    {
        var h = HandlerConductores([Cond(1, "  "), Cond(2, "55", "Primero"), Cond(3, "55", "Segundo")], []);

        var r = await h.Handle(new clsSincronizarConductoresCommand());

        Assert.Equal("Segundo", _conductores.Single().Nombres);
        Assert.Equal(3, r.Total);
        Assert.Equal(2, r.Omitidos);
    }

    // ---------- Tanqueos ----------

    private static readonly Guid VEHICULO = Guid.NewGuid();
    private readonly Mock<ITanqueosLegadoPort> _tanqueosLegado = new();
    private readonly Mock<IVehiculosSincronizacionRepository> _vehiculos = new();
    private readonly Mock<ITanqueosSincronizacionRepository> _tanqueosRepo = new();
    private IReadOnlyList<clsTanqueoUpsertDto> _tanqueos = [];

    private clsSincronizarTanqueosCommandHandler HandlerTanqueos(
        IEnumerable<clsTanqueoLegadoDto> distracom, IEnumerable<clsTanqueoLegadoDto> terpel)
    {
        _tanqueosLegado.Setup(l => l.ListarDistracomAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(distracom.ToList());
        _tanqueosLegado.Setup(l => l.ListarTerpelAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(terpel.ToList());
        _vehiculos.Setup(v => v.ObtenerMapaPlacaAIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["ABC123"] = VEHICULO });
        _tanqueosRepo.Setup(r => r.UpsertAsync(It.IsAny<IReadOnlyList<clsTanqueoUpsertDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsTanqueoUpsertDto>, CancellationToken>((l, _) => _tanqueos = l)
            .ReturnsAsync((IReadOnlyList<clsTanqueoUpsertDto> l, CancellationToken _) => new clsResultadoUpsertDto(l.Count, 0, []));
        return new(_tanqueosLegado.Object, _vehiculos.Object, _tanqueosRepo.Object,
            Options.Create(new clsSyncOptions()), NullLogger<clsSincronizarTanqueosCommandHandler>.Instance);
    }

    private static clsTanqueoLegadoDto Tanqueo(FuenteTanqueo fuente, long id, string placa) =>
        new(fuente, id, placa, DateTimeOffset.UtcNow, 50m, 9_000m, 450_000m, "ACPM", null, "Bogota", null, null, null, null, null, null);

    [Fact]
    public async Task Unifica_Distracom_y_Terpel_y_enlaza_el_vehiculo_por_placa()
    {
        var h = HandlerTanqueos(
            [Tanqueo(FuenteTanqueo.Distracom, 1, "ABC123")],
            [Tanqueo(FuenteTanqueo.Terpel, 1, "abc123")]);

        var r = await h.Handle(new clsSincronizarTanqueosCommand());

        Assert.Equal(2, _tanqueos.Count);
        Assert.All(_tanqueos, t => Assert.Equal(VEHICULO, t.VehiculoId));
        Assert.Equal([FuenteTanqueo.Distracom, FuenteTanqueo.Terpel], _tanqueos.Select(t => t.Origen.Fuente));
        Assert.Equal(2, r.Total);
        Assert.Equal(1, r.Extras["distracom"]);
        Assert.Equal(1, r.Extras["terpel"]);
    }

    [Fact]
    public async Task Un_tanqueo_de_una_placa_fuera_de_la_flota_se_conserva_con_VehiculoId_null()
    {
        var h = HandlerTanqueos([Tanqueo(FuenteTanqueo.Distracom, 1, "ZZZ999"), Tanqueo(FuenteTanqueo.Distracom, 2, "ABC123")], []);

        var r = await h.Handle(new clsSincronizarTanqueosCommand());

        Assert.Equal(2, _tanqueos.Count);          // no se descarta: se subestimaria el consumo
        Assert.Null(_tanqueos.Single(t => t.Origen.FuenteId == 1).VehiculoId);
        Assert.Equal(1, r.Extras["sinVehiculo"]);
        Assert.Equal(0, r.Omitidos);
    }

    [Fact]
    public async Task Los_errores_de_filas_se_agregan_y_el_resto_se_cuenta_como_insertado()
    {
        var h = HandlerTanqueos([Tanqueo(FuenteTanqueo.Distracom, 1, "ABC123"), Tanqueo(FuenteTanqueo.Distracom, 2, "ABC123")], []);
        _tanqueosRepo.Setup(r => r.UpsertAsync(It.IsAny<IReadOnlyList<clsTanqueoUpsertDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsResultadoUpsertDto(1, 0, [new clsErrorSincronizacion("distracom 2 (ABC123)", "valor fuera de rango")]));

        var r = await h.Handle(new clsSincronizarTanqueosCommand());

        Assert.Equal(1, r.ErroresTotal);
        Assert.Equal(1, r.Insertados);
        Assert.True(r.TieneErrores);
    }

    [Fact]
    public async Task Usa_la_fecha_desde_de_la_configuracion()
    {
        var h = HandlerTanqueos([], []);

        await h.Handle(new clsSincronizarTanqueosCommand());

        _tanqueosLegado.Verify(l => l.ListarDistracomAsync(new DateTime(2026, 1, 1), It.IsAny<CancellationToken>()), Times.Once);
        _tanqueosLegado.Verify(l => l.ListarTerpelAsync(new DateTime(2026, 1, 1), It.IsAny<CancellationToken>()), Times.Once);
    }
}
