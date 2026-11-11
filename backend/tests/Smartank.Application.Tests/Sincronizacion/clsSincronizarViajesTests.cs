using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Domain.Enums;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.Tests.Sincronizacion;

public class clsSincronizarViajesTests
{
    // 2026-06-01 14:30 UTC = 09:30 en Colombia.
    private static readonly DateTimeOffset AHORA = new(2026, 6, 1, 14, 30, 0, TimeSpan.Zero);
    private static readonly Guid VEHICULO = Guid.NewGuid();

    private readonly Mock<IFletesLegadoPort> _fletes = new();
    private readonly Mock<IManifiestosCerradosPort> _cerrados = new();
    private readonly Mock<IVehiculosSincronizacionRepository> _vehiculos = new();
    private readonly Mock<IViajesSincronizacionRepository> _viajes = new();
    private IReadOnlyList<clsViajeUpsertDto> _escritos = [];

    public clsSincronizarViajesTests()
    {
        _cerrados.Setup(c => c.ListarDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string>());
        _vehiculos.Setup(v => v.ObtenerMapaPlacaAIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["ABC123"] = VEHICULO });
        _viajes.Setup(v => v.UpsertAsync(It.IsAny<IReadOnlyList<clsViajeUpsertDto>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<clsViajeUpsertDto>, CancellationToken>((lista, _) => _escritos = lista)
            .ReturnsAsync((IReadOnlyList<clsViajeUpsertDto> lista, CancellationToken _) => new clsResultadoUpsertDto(lista.Count, 0, []));
    }

    private clsSincronizarViajesCommandHandler Handler() => new(
        _fletes.Object, _cerrados.Object, _vehiculos.Object, _viajes.Object,
        Options.Create(new clsSyncOptions()), new clsRelojFalso(AHORA), NullLogger<clsSincronizarViajesCommandHandler>.Instance);

    private static clsFleteLegadoDto Flete(
        long id = 1, string placa = "ABC123", string? numeroFo = "301453730", bool borrado = false,
        DateTimeOffset? despacho = null, string? nombreRuta = "BOGOTA - CALI", string? codigoRuta = "R1") =>
        new(id, placa, "PROG", null, "123", "TRANS", codigoRuta, nombreRuta, "O", "D", numeroFo, 10m, 1m, 2m, 3m, 4m, 5m,
            despacho ?? AHORA.AddHours(-2), true, false, false, false, borrado);

    private void ConFletes(params clsFleteLegadoDto[] fletes) =>
        _fletes.Setup(f => f.ListarDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(fletes);

    [Fact]
    public async Task Deriva_el_estado_de_cada_flete_segun_borrado_cierre_y_fecha()
    {
        _cerrados.Setup(c => c.ListarDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "222" });
        ConFletes(
            Flete(1, numeroFo: "0000111", borrado: true),
            Flete(2, numeroFo: "0000222"),
            Flete(3, numeroFo: "333", despacho: AHORA.AddHours(-1)),
            Flete(4, numeroFo: "444", despacho: AHORA.AddHours(1)));

        await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.Equal(
            [EstadoViaje.Descartado, EstadoViaje.Cerrado, EstadoViaje.EnRuta, EstadoViaje.Programado],
            _escritos.OrderBy(e => e.FoId).Select(e => e.Estado));
    }

    [Fact]
    public async Task El_cruce_con_los_manifiestos_cerrados_ignora_los_ceros_a_la_izquierda()
    {
        _cerrados.Setup(c => c.ListarDesdeAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new HashSet<string> { "301453730" });
        ConFletes(Flete(numeroFo: "000000301453730"));

        await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.Equal(EstadoViaje.Cerrado, _escritos.Single().Estado);
    }

    [Fact]
    public async Task El_manifiesto_y_el_numero_de_FO_son_el_FO_sin_ceros()
    {
        ConFletes(Flete(numeroFo: "000000301453730"));

        await Handler().Handle(new clsSincronizarViajesCommand());

        var v = _escritos.Single();
        Assert.Equal("301453730", v.Manifiesto);
        Assert.Equal("301453730", v.NumeroFo);
    }

    // Despacho a las 10:00 de Colombia = 15:00Z; "ahora" es 14:30Z, asi que aun es programado y sin InicioAt.
    [Fact]
    public async Task La_hora_del_legado_se_interpreta_como_Colombia_y_un_despacho_futuro_queda_programado_sin_inicio()
    {
        ConFletes(Flete(despacho: clsHoraColombia.AUtc(new DateTime(2026, 6, 1, 10, 0, 0))));

        await Handler().Handle(new clsSincronizarViajesCommand());

        var v = _escritos.Single();
        Assert.Equal(EstadoViaje.Programado, v.Estado);
        Assert.Null(v.InicioAt);
    }

    [Fact]
    public async Task Un_viaje_en_ruta_usa_la_fecha_de_despacho_como_inicio()
    {
        var despacho = AHORA.AddHours(-3);
        ConFletes(Flete(despacho: despacho));

        await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.Equal(despacho, _escritos.Single().InicioAt);
    }

    [Fact]
    public async Task Omite_los_FO_cuyo_cabezote_no_existe_en_la_flota_y_no_los_escribe()
    {
        ConFletes(Flete(1, placa: "ABC123"), Flete(2, placa: "ZZZ999"), Flete(3, placa: "ZZZ998"));

        var r = await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.Equal(3, r.Total);
        Assert.Equal(2, r.Omitidos);
        Assert.Equal(1, r.Insertados);
        Assert.Single(_escritos);
        Assert.Equal(VEHICULO, _escritos.Single().VehiculoId);
    }

    [Fact]
    public async Task La_placa_se_cruza_sin_distinguir_mayusculas()
    {
        ConFletes(Flete(placa: "abc123"));

        var r = await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.Equal(0, r.Omitidos);
    }

    [Fact]
    public async Task Los_errores_del_repositorio_se_agregan_sin_abortar_y_el_detalle_se_acota()
    {
        ConFletes(Flete(1), Flete(2));
        var errores = Enumerable.Range(1, 25).Select(i => new clsErrorSincronizacion($"FO {i}", "duplicado")).ToList();
        _viajes.Setup(v => v.UpsertAsync(It.IsAny<IReadOnlyList<clsViajeUpsertDto>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsResultadoUpsertDto(1, 0, errores));

        var r = await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.True(r.TieneErrores);
        Assert.Equal(25, r.ErroresTotal);
        Assert.Equal(clsResultadoSincronizacion.MAX_ERRORES_DETALLADOS, r.Errores.Count);
        Assert.Equal(1, r.Insertados);
    }

    [Fact]
    public async Task Un_FO_repetido_en_la_carga_se_escribe_una_sola_vez()
    {
        ConFletes(Flete(7, numeroFo: "1"), Flete(7, numeroFo: "2"));

        await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.Equal("2", _escritos.Single().NumeroFo);
    }

    [Fact]
    public async Task La_ruta_usa_el_nombre_y_si_falta_el_codigo()
    {
        ConFletes(Flete(1, nombreRuta: "", codigoRuta: "R-77"));

        await Handler().Handle(new clsSincronizarViajesCommand());

        Assert.Equal("R-77", _escritos.Single().Ruta);
    }

    [Fact]
    public async Task Usa_la_fecha_configurada_y_el_comando_puede_sobrescribirla()
    {
        ConFletes();
        var h = Handler();

        await h.Handle(new clsSincronizarViajesCommand());
        _fletes.Verify(f => f.ListarDesdeAsync(new DateTime(2026, 1, 1), It.IsAny<CancellationToken>()), Times.Once);

        await h.Handle(new clsSincronizarViajesCommand(new DateTime(2026, 5, 1)));
        _fletes.Verify(f => f.ListarDesdeAsync(new DateTime(2026, 5, 1), It.IsAny<CancellationToken>()), Times.Once);
    }
}
