using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Autonomia;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Tests.Autorizaciones;
using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Application.Tests.Autonomia;

public class clsCalcularAutonomiaCommandTests
{
    private readonly Mock<IContextoTanqueoRepository> _contexto = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly clsParametrosNegocio _parametros = new();
    private readonly clsRelojFijo _reloj = new(clsDatosPrueba.AHORA);

    private clsCalcularAutonomiaCommandHandler Handler() =>
        new(_contexto.Object, _uow.Object, Options.Create(_parametros), _reloj);

    private (clsViaje Viaje, clsVehiculo Vehiculo) Escenario(clsVehiculo vehiculo)
    {
        var viaje = clsDatosPrueba.Viaje(vehiculo.Id);
        _contexto.Setup(c => c.ObtenerViajeAsync(viaje.Id, It.IsAny<CancellationToken>())).ReturnsAsync(viaje);
        _contexto.Setup(c => c.ObtenerVehiculoAsync(vehiculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(vehiculo);
        return (viaje, vehiculo);
    }

    [Fact]
    public async Task Calcula_con_el_rendimiento_historico_y_persiste_en_el_viaje()
    {
        // 100 gal x 5 km/gal = 500; -2 % = 490 km (el golden de autonomia.test.ts).
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivel: 100m, historico: 5, tolerancia: 0.02));

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(490, r.AutonomiaKm);
        Assert.Equal(BaseRendimiento.Historico, r.BaseRendimiento);
        Assert.Equal(5, r.RendimientoUsado);
        Assert.Equal(0.02, r.Tolerancia);
        Assert.Equal(490, viaje.AutonomiaEstimadaKm);
        Assert.Equal(490, r.Viaje.AutonomiaEstimadaKm);
        Assert.Equal(viaje.Id, r.Viaje.Id);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Sin_historico_usa_el_esperado()
    {
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivel: 100m, historico: null, esperado: 4, tolerancia: 0));

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(400, r.AutonomiaKm);
        Assert.Equal(BaseRendimiento.Esperado, r.BaseRendimiento);
    }

    [Fact]
    public async Task Sin_historico_ni_esperado_usa_el_rendimiento_default_de_negocio()
    {
        _parametros.RendimientoDefaultKmGal = 8;
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivel: 100m, historico: null, esperado: null, tolerancia: 0.02));

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(784, r.AutonomiaKm); // 100 x 8 x 0.98
        Assert.Equal(BaseRendimiento.Default, r.BaseRendimiento);
        Assert.Equal(8, r.RendimientoUsado);
    }

    [Fact]
    public async Task Redondea_la_autonomia_a_dos_decimales()
    {
        // 33.333 x 5 = 166.665; x 0.98 = 163.3317 -> 163.33
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivel: 33.333m, historico: 5, tolerancia: 0.02));

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(163.33, r.AutonomiaKm);
        Assert.Equal(163.33, viaje.AutonomiaEstimadaKm);
    }

    [Fact]
    public async Task Con_el_tanque_vacio_la_autonomia_es_cero()
    {
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivel: 0m));

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(0, r.AutonomiaKm);
    }

    [Fact]
    public async Task Viaje_inexistente_lanza_404()
    {
        _contexto.Setup(c => c.ObtenerViajeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsViaje?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsCalcularAutonomiaCommand(Guid.NewGuid())));

        Assert.Equal("VIAJE_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Vehiculo_inexistente_lanza_404()
    {
        var viaje = clsDatosPrueba.Viaje();
        _contexto.Setup(c => c.ObtenerViajeAsync(viaje.Id, It.IsAny<CancellationToken>())).ReturnsAsync(viaje);
        _contexto.Setup(c => c.ObtenerVehiculoAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsVehiculo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id)));

        Assert.Equal("VEHICULO_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Sin_nivel_de_tanque_lanza_409_SIN_TELEMETRIA_NIVEL_y_no_guarda()
    {
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivel: null));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id)));

        Assert.Equal("SIN_TELEMETRIA_NIVEL", ex.Code);
        Assert.Null(viaje.AutonomiaEstimadaKm);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_lectura_mas_vieja_que_el_maximo_lanza_409_TELEMETRIA_OBSOLETA_y_no_guarda()
    {
        var lecturaVieja = clsDatosPrueba.AHORA.AddHours(-168).AddMinutes(-1);
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivelAt: lecturaVieja));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id)));

        Assert.Equal("TELEMETRIA_OBSOLETA", ex.Code);
        Assert.Null(viaje.AutonomiaEstimadaKm);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_lectura_exactamente_en_el_limite_de_168_horas_se_acepta()
    {
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivelAt: clsDatosPrueba.AHORA.AddHours(-168)));

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(490, r.AutonomiaKm);
    }

    [Fact]
    public async Task El_maximo_de_antiguedad_es_configurable()
    {
        _parametros.NivelMaxAntiguedadHoras = 1;
        var (viaje, _) = Escenario(clsDatosPrueba.Vehiculo(nivelAt: clsDatosPrueba.AHORA.AddHours(-2)));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id)));

        Assert.Equal("TELEMETRIA_OBSOLETA", ex.Code);
    }

    [Fact]
    public void El_maximo_de_antiguedad_por_defecto_es_168_horas()
    {
        Assert.Equal(168, new clsParametrosNegocio().NivelMaxAntiguedadHoras);
    }

    [Fact]
    public async Task El_nivel_se_toma_de_la_ultima_lectura_aunque_la_posicion_sea_mas_vieja()
    {
        // La antiguedad se mide con NivelAt (lectura de nivel), no con la ubicacion.
        var vehiculo = clsDatosPrueba.Vehiculo(nivelAt: clsDatosPrueba.AHORA.AddMinutes(-5));
        vehiculo.UbicacionAt = clsDatosPrueba.AHORA.AddDays(-30);
        var (viaje, _) = Escenario(vehiculo);

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(490, r.AutonomiaKm);
    }

    [Fact]
    public async Task Una_lectura_sin_fecha_no_se_puede_juzgar_y_se_acepta()
    {
        var vehiculo = clsDatosPrueba.Vehiculo();
        vehiculo.NivelAt = null;
        var (viaje, _) = Escenario(vehiculo);

        var r = await Handler().Handle(new clsCalcularAutonomiaCommand(viaje.Id));

        Assert.Equal(490, r.AutonomiaKm);
    }
}
