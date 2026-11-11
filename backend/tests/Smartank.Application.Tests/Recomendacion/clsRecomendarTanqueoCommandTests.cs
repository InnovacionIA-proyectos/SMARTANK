using Microsoft.Extensions.Options;
using Moq;
using Smartank.Application.CasosDeUso.Autonomia;
using Smartank.Application.CasosDeUso.Autonomia.DTOs;
using Smartank.Application.CasosDeUso.Autonomia.Mappers;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.CasosDeUso.Recomendacion;
using Smartank.Application.CasosDeUso.Recomendacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Configuracion;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Application.Tests.Autorizaciones;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Autorizaciones.Reglas;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Application.Tests.Recomendacion;

/// <summary>
/// Recomendacion sobre una ruta en el ecuador de (0,0) a (0,2) = 222.39 km con el vehiculo en el km 50 (le faltan 172.39 km).
/// Las estaciones se ubican por "km sobre la ruta" (1 grado de longitud = 111.19 km).
/// </summary>
public class clsRecomendarTanqueoCommandTests
{
    private const double KM_POR_GRADO = 111.19492664455873;
    private static readonly Guid USUARIO = Guid.NewGuid();

    private readonly Mock<ICommandHandler<clsCalcularAutonomiaCommand, clsAutonomiaDto>> _autonomia = new();
    private readonly Mock<IContextoTanqueoRepository> _contexto = new();
    private readonly Mock<IRecomendacionRepository> _recomendacion = new();
    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();
    private readonly clsParametrosNegocio _parametros = new();
    private readonly clsRelojFijo _reloj = new(clsDatosPrueba.AHORA);

    private readonly clsViaje _viaje;
    private readonly clsVehiculo _vehiculo;
    private List<clsEstacion> _estaciones = [];
    private double[][]? _polilinea = [[0, 0], [0, 2]];

    public clsRecomendarTanqueoCommandTests()
    {
        _vehiculo = clsDatosPrueba.Vehiculo(nivel: 100m, historico: 5, tolerancia: 0.02, capacidad: 300m, lat: 0, lng: 50 / KM_POR_GRADO);
        _viaje = clsDatosPrueba.Viaje(_vehiculo.Id, autonomia: 100);
        _usuario.SetupGet(u => u.UsuarioId).Returns(USUARIO);
        _contexto.Setup(c => c.ObtenerViajeAsync(_viaje.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_viaje);
        _contexto.Setup(c => c.ObtenerVehiculoAsync(_vehiculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vehiculo);
        _recomendacion.Setup(r => r.ListarEstacionesConPrecioAcpmAsync(It.IsAny<CancellationToken>())).ReturnsAsync(() => _estaciones);
        _recomendacion.Setup(r => r.ObtenerPolilineaDeViajeAsync(_viaje.Id, It.IsAny<CancellationToken>())).ReturnsAsync(() => _polilinea);
    }

    private clsRecomendarTanqueoCommandHandler Handler() => new(
        _autonomia.Object, _contexto.Object, _recomendacion.Object, _autorizaciones.Object, _auditoria.Object,
        _uow.Object, _usuario.Object, Options.Create(_parametros), _reloj);

    /// <summary>Estacion a <paramref name="km"/> del origen sobre la ruta (o desplazada al norte).</summary>
    private static clsEstacion Est(double km, decimal precio, double lat = 0) =>
        clsDatosPrueba.Estacion(lat, km / KM_POR_GRADO, precio, nombre: $"EDS km {km}");

    private clsAutorizacionTanqueo? _agregada;

    private void CapturarAgregadas() =>
        _autorizaciones.Setup(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()))
            .Callback<clsAutorizacionTanqueo>(a => { a.Id = Guid.NewGuid(); _agregada = a; });

    // ---- Casos principales -------------------------------------------------------------------------------------------

    [Fact]
    public async Task Sin_tanqueo_requerido_igual_genera_la_propuesta_y_explica_el_motivo()
    {
        _viaje.AutonomiaEstimadaKm = 300; // cubre los 172.39 km restantes
        _estaciones = [Est(100, 9500), Est(150, 9300)];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.False(r.RequiereTanqueo);
        Assert.Equal("La autonomía (300 km) cubre la ruta restante (172 km).", r.Motivo);
        Assert.NotNull(_agregada); // la propuesta se genera igual: el panel decide si se habilita Autorizar
        Assert.Equal(r.Autorizacion.Id, _agregada!.Id);
        Assert.Equal(172.4, r.RutaKmRestante);
        Assert.Equal(300, r.AutonomiaKm);
    }

    [Fact]
    public async Task Elige_la_estacion_mas_barata_de_la_zona_de_repostaje_y_arma_la_propuesta()
    {
        // Autonomia 100, zona = 60..100 km por delante (km 110..150 de la ruta). Fuera de zona hay una mas barata.
        var barataFueraDeZona = Est(100, 7000);
        var zonaBarata = Est(120, 9300);
        var zonaCara = Est(140, 9800);
        _estaciones = [barataFueraDeZona, zonaBarata, zonaCara];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.True(r.RequiereTanqueo);
        Assert.Null(r.Motivo);
        Assert.True(r.SobreRuta);
        Assert.Equal(2, r.EstacionesCandidatas);
        Assert.Equal(zonaBarata.Id, r.EstacionElegida.Id);
        Assert.Equal(9300m, r.PrecioTecho);
        // Deficit = 172.39 - 100 = 72.39 km; rendimiento efectivo 5 x 0.98 = 4.9; 72.39 / 4.9 = 14.8 -> 15 galones.
        Assert.Equal(15m, r.GalonesMax);
        // Ahorro = (9800 - 9300) x 15.
        Assert.Equal(7500m, r.DeltaAhorroEstimado);

        Assert.Equal(EstadoAutorizacion.Propuesta, _agregada!.Estado);
        Assert.Equal(zonaBarata.Id, _agregada.EstacionId);
        Assert.Equal(_viaje.Id, _agregada.ViajeId);
        Assert.Equal(_vehiculo.Id, _agregada.VehiculoId);
        Assert.Equal(15m, _agregada.GalonesMax);
        Assert.Equal(9300m, _agregada.PrecioTecho);
        Assert.Equal(7500m, _agregada.DeltaAhorroEstimado);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task La_propuesta_vence_12_horas_despues_de_generarse()
    {
        _estaciones = [Est(120, 9300)];
        CapturarAgregadas();

        await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.Equal(clsDatosPrueba.AHORA, _agregada!.FechaInicio);
        Assert.Equal(clsDatosPrueba.AHORA.AddHours(12), _agregada.FechaFin);
    }

    [Fact]
    public async Task Sin_estaciones_en_la_zona_usa_las_alcanzables_hacia_adelante()
    {
        var cerca = Est(70, 9400);   // adelante 20: debajo de la zona
        var cerca2 = Est(100, 9600); // adelante 50
        var lejos = Est(190, 7000);  // adelante 140: fuera de la autonomia
        _estaciones = [cerca, cerca2, lejos];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.Equal(2, r.EstacionesCandidatas);
        Assert.Equal(cerca.Id, r.EstacionElegida.Id);
        Assert.True(r.SobreRuta);
    }

    [Fact]
    public async Task Si_ninguna_alcanza_toma_la_mas_cercana_hacia_adelante()
    {
        _viaje.AutonomiaEstimadaKm = 20;
        var masCercana = Est(100, 9600);
        var masBarata = Est(150, 7000);
        _estaciones = [masBarata, masCercana];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.Equal(1, r.EstacionesCandidatas);
        Assert.Equal(masCercana.Id, r.EstacionElegida.Id);
    }

    [Fact]
    public async Task Ignora_las_estaciones_detras_del_vehiculo()
    {
        var detras = Est(20, 5000);
        var adelante = Est(120, 9500);
        _estaciones = [detras, adelante];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.Equal(adelante.Id, r.EstacionElegida.Id);
    }

    [Fact]
    public async Task Sin_estaciones_sobre_la_ruta_cae_a_linea_recta_y_marca_sobre_ruta_falso()
    {
        // 0.2 grados al norte = 22 km de la via: no cuenta sobre la ruta pero esta a 24 km en linea recta.
        var fueraDeVia = Est(60, 9400, lat: 0.2);
        _estaciones = [fueraDeVia];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.False(r.SobreRuta);
        Assert.Equal(fueraDeVia.Id, r.EstacionElegida.Id);
        Assert.Equal(172.4, r.RutaKmRestante); // la ruta restante igual se midio
        Assert.True(r.RequiereTanqueo);
    }

    [Fact]
    public async Task Sin_ruta_propone_tanqueo_sin_ruta_restante_y_con_galones_por_defecto()
    {
        _polilinea = null;
        var cerca = Est(60, 9400, lat: 0.2);
        _estaciones = [cerca, Est(100, 8000, lat: 1.0)];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.True(r.RequiereTanqueo);
        Assert.False(r.SobreRuta);
        Assert.Null(r.RutaKmRestante);
        Assert.Null(r.Motivo);
        Assert.Equal(cerca.Id, r.EstacionElegida.Id); // solo la alcanzable en linea recta
        Assert.Equal(40m, r.GalonesMax);              // DefaultGalonesAutorizacion
    }

    [Fact]
    public async Task Sin_posicion_del_vehiculo_propone_tanqueo_con_todas_las_estaciones()
    {
        _vehiculo.UltimaLat = null;
        _vehiculo.UltimaLng = null;
        _estaciones = [Est(60, 9400), Est(100, 8000, lat: 1.0)];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.True(r.RequiereTanqueo);
        Assert.Equal(2, r.EstacionesCandidatas);
        Assert.Null(r.RutaKmRestante);
    }

    // ---- Galones ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Los_galones_solicitados_mandan_sobre_el_calculo()
    {
        _estaciones = [Est(120, 9300)];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id, 22.5m));

        Assert.Equal(22.5m, r.GalonesMax);
        Assert.Equal(22.5m, _agregada!.GalonesMax);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Galones_no_positivos_lanzan_400_sin_tocar_nada(int galones)
    {
        var ex = await Assert.ThrowsAsync<clsValidationFailedException>(
            () => Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id, galones)));

        Assert.Equal("GALONES_INVALIDOS", ex.Code);
        _contexto.Verify(c => c.ObtenerViajeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Los_galones_se_topan_al_espacio_libre_del_tanque()
    {
        // Capacidad 90, nivel 80: solo caben 10 galones aunque el deficit pida 15.
        _vehiculo.CapacidadTanqueGal = 90m;
        _vehiculo.NivelTanqueActual = 80m;
        _estaciones = [Est(120, 9300)];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.Equal(10m, r.GalonesMax);
    }

    [Fact]
    public async Task Con_el_tanque_lleno_propone_el_minimo_de_un_galon()
    {
        _vehiculo.CapacidadTanqueGal = 80m;
        _vehiculo.NivelTanqueActual = 80m;
        _estaciones = [Est(120, 9300)];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.Equal(1m, r.GalonesMax);
    }

    // ---- Propuesta existente: se actualiza en vez de duplicar -----------------------------------------------------------

    [Fact]
    public async Task Si_el_viaje_ya_tiene_una_propuesta_la_actualiza_en_vez_de_insertar_otra()
    {
        var existente = clsDatosPrueba.Autorizacion(EstadoAutorizacion.Propuesta, galonesMax: 99m, viajeId: _viaje.Id, vehiculoId: _vehiculo.Id);
        existente.PrecioTecho = 12345m;
        existente.FechaInicio = clsDatosPrueba.AHORA.AddDays(-3);
        existente.FechaFin = clsDatosPrueba.AHORA.AddDays(-2);
        var idOriginal = existente.Id;
        _autorizaciones.Setup(a => a.ObtenerPropuestaDeViajeAsync(_viaje.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existente);
        var elegida = Est(120, 9300);
        _estaciones = [elegida, Est(140, 9800)];

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        _autorizaciones.Verify(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()), Times.Never);
        Assert.Equal(idOriginal, r.Autorizacion.Id);
        Assert.Equal(EstadoAutorizacion.Propuesta, existente.Estado);
        Assert.Equal(elegida.Id, existente.EstacionId);
        Assert.Equal(15m, existente.GalonesMax);
        Assert.Equal(9300m, existente.PrecioTecho);
        Assert.Equal(7500m, existente.DeltaAhorroEstimado);
        Assert.Equal(clsDatosPrueba.AHORA, existente.FechaInicio);
        Assert.Equal(clsDatosPrueba.AHORA.AddHours(12), existente.FechaFin);
        _auditoria.Verify(a => a.RegistrarAsync(
            USUARIO, "recomendar_tanqueo", "autorizaciones_tanqueo", idOriginal,
            It.Is<object?>(d => clsDatosPrueba.Json(d) == "{\"estado\":\"propuesta\",\"actualizada\":true}"),
            It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_propuesta_nueva_se_audita_como_no_actualizada()
    {
        _estaciones = [Est(120, 9300)];
        CapturarAgregadas();

        await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        _auditoria.Verify(a => a.RegistrarAsync(
            USUARIO, "recomendar_tanqueo", "autorizaciones_tanqueo", _agregada!.Id,
            It.Is<object?>(d => clsDatosPrueba.Json(d) == "{\"estado\":\"propuesta\",\"actualizada\":false}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Ante_una_carrera_REGISTRO_DUPLICADO_relee_la_ganadora_y_la_actualiza()
    {
        var ganadora = clsDatosPrueba.Autorizacion(EstadoAutorizacion.Propuesta, galonesMax: 99m, viajeId: _viaje.Id, vehiculoId: _vehiculo.Id);
        // La primera lectura no ve propuesta; despues del choque con el indice unico ya aparece la de la otra peticion.
        _autorizaciones.SetupSequence(a => a.ObtenerPropuestaDeViajeAsync(_viaje.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((clsAutorizacionTanqueo?)null)
            .ReturnsAsync(ganadora);
        _uow.SetupSequence(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("REGISTRO_DUPLICADO", "duplicado"))
            .ReturnsAsync(1);
        var elegida = Est(120, 9300);
        _estaciones = [elegida];
        clsAutorizacionTanqueo? insertada = null;
        _autorizaciones.Setup(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>())).Callback<clsAutorizacionTanqueo>(a => insertada = a);

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        _autorizaciones.Verify(a => a.Descartar(insertada!), Times.Once); // para que el guardado siguiente no reintente el INSERT
        Assert.Equal(ganadora.Id, r.Autorizacion.Id);
        Assert.Equal(elegida.Id, ganadora.EstacionId);
        Assert.Equal(15m, ganadora.GalonesMax);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Un_REGISTRO_DUPLICADO_sin_propuesta_ganadora_se_propaga()
    {
        _estaciones = [Est(120, 9300)];
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("REGISTRO_DUPLICADO", "duplicado"));

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id)));

        Assert.Equal("REGISTRO_DUPLICADO", ex.Code);
    }

    // ---- Validaciones y autonomia previa ---------------------------------------------------------------------------

    [Fact]
    public async Task Sin_estaciones_con_precio_lanza_409()
    {
        _estaciones = [];

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id)));

        Assert.Equal("SIN_ESTACIONES_CON_PRECIO", ex.Code);
        _autorizaciones.Verify(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task Viaje_inexistente_lanza_404()
    {
        _contexto.Setup(c => c.ObtenerViajeAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsViaje?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsRecomendarTanqueoCommand(Guid.NewGuid())));

        Assert.Equal("VIAJE_NO_ENCONTRADO", ex.Code);
    }

    [Fact]
    public async Task Si_el_viaje_no_tiene_autonomia_la_calcula_primero()
    {
        _viaje.AutonomiaEstimadaKm = null;
        _autonomia.Setup(a => a.Handle(It.Is<clsCalcularAutonomiaCommand>(c => c.ViajeId == _viaje.Id), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsAutonomiaDto(clsViajeAutonomiaMapper.ToDto(_viaje), 100, BaseRendimiento.Historico, 5, 0.02));
        _estaciones = [Est(120, 9300)];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        _autonomia.Verify(a => a.Handle(It.IsAny<clsCalcularAutonomiaCommand>(), It.IsAny<CancellationToken>()), Times.Once);
        Assert.Equal(100, r.AutonomiaKm);
    }

    [Fact]
    public async Task Si_el_viaje_ya_tiene_autonomia_no_la_recalcula()
    {
        _estaciones = [Est(120, 9300)];
        CapturarAgregadas();

        await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        _autonomia.Verify(a => a.Handle(It.IsAny<clsCalcularAutonomiaCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Si_calcular_la_autonomia_falla_por_telemetria_obsoleta_el_error_se_propaga()
    {
        _viaje.AutonomiaEstimadaKm = null;
        _autonomia.Setup(a => a.Handle(It.IsAny<clsCalcularAutonomiaCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new clsConflictException("TELEMETRIA_OBSOLETA", "vieja"));
        _estaciones = [Est(120, 9300)];

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id)));

        Assert.Equal("TELEMETRIA_OBSOLETA", ex.Code);
        _autorizaciones.Verify(a => a.Agregar(It.IsAny<clsAutorizacionTanqueo>()), Times.Never);
    }

    [Fact]
    public async Task La_respuesta_incluye_la_estacion_elegida_completa()
    {
        var elegida = Est(120, 9300);
        elegida.CodigoExterno = "EDS0001";
        _estaciones = [elegida];
        CapturarAgregadas();

        var r = await Handler().Handle(new clsRecomendarTanqueoCommand(_viaje.Id));

        Assert.Equal("EDS0001", r.EstacionElegida.CodigoExterno);
        Assert.Equal(elegida.Nombre, r.EstacionElegida.Nombre);
        Assert.Equal(9300m, r.EstacionElegida.PrecioAcpm);
        Assert.True(r.EstacionElegida.SoportaPreautorizacion);
    }
}
