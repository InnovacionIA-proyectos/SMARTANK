using System.Text.Json;
using Moq;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Application.Tests.Autorizaciones;

public class clsPreautorizarEdsCommandTests
{
    private static readonly Guid USUARIO = Guid.NewGuid();

    private readonly Mock<IAutorizacionesRepository> _autorizaciones = new();
    private readonly Mock<IContextoTanqueoRepository> _contexto = new();
    private readonly Mock<IEdsPort> _eds = new();
    private readonly Mock<IAuditoriaPort> _auditoria = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ICurrentUserPort> _usuario = new();

    private readonly clsEstacion _estacion = clsDatosPrueba.Estacion(soportaPreautorizacion: true);
    private readonly clsVehiculo _vehiculo = clsDatosPrueba.Vehiculo();

    public clsPreautorizarEdsCommandTests() => _usuario.SetupGet(u => u.UsuarioId).Returns(USUARIO);

    private clsPreautorizarEdsCommandHandler Handler() =>
        new(_autorizaciones.Object, _contexto.Object, _eds.Object, _auditoria.Object, _uow.Object, _usuario.Object);

    private clsAutorizacionTanqueo Existente(EstadoAutorizacion estado = EstadoAutorizacion.Autorizada)
    {
        var a = clsDatosPrueba.Autorizacion(estado, galonesMax: 35m, estacionId: _estacion.Id, vehiculoId: _vehiculo.Id);
        _autorizaciones.Setup(r => r.ObtenerAsync(a.Id, It.IsAny<CancellationToken>())).ReturnsAsync(a);
        _contexto.Setup(c => c.ObtenerEstacionAsync(_estacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_estacion);
        _contexto.Setup(c => c.ObtenerVehiculoAsync(_vehiculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_vehiculo);
        return a;
    }

    private static clsPreautorizacionEdsResultado Simulada() =>
        new(true, "SIMULADO-ABC123-1", true, clsDatosPrueba.AHORA);

    [Fact]
    public async Task Pre_autoriza_con_los_datos_de_la_autorizacion_y_guarda_la_confirmacion_como_json_snake_case()
    {
        var a = Existente();
        clsPreautorizacionEdsSolicitud? enviada = null;
        _eds.Setup(e => e.RegistrarPreautorizacionAsync(It.IsAny<clsPreautorizacionEdsSolicitud>(), It.IsAny<CancellationToken>()))
            .Callback<clsPreautorizacionEdsSolicitud, CancellationToken>((s, _) => enviada = s)
            .ReturnsAsync(Simulada());

        var r = await Handler().Handle(new clsPreautorizarEdsCommand(a.Id));

        Assert.Equal(new clsPreautorizacionEdsSolicitud("ABC123", 35m, a.FechaInicio, a.FechaFin), enviada);
        Assert.True(r.Eds.Simulado);
        Assert.Equal("SIMULADO-ABC123-1", r.Eds.Referencia);

        using var json = JsonDocument.Parse(a.EdsConfirmacion!);
        Assert.True(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.True(json.RootElement.GetProperty("simulado").GetBoolean());
        Assert.Equal("SIMULADO-ABC123-1", json.RootElement.GetProperty("referencia").GetString());
        Assert.True(json.RootElement.TryGetProperty("recibido_at", out _));

        // La respuesta expone la confirmacion como objeto, no como texto.
        Assert.Equal(JsonValueKind.Object, r.Autorizacion.EdsConfirmacion!.Value.ValueKind);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Audita_la_referencia_de_la_confirmacion()
    {
        var a = Existente();
        _eds.Setup(e => e.RegistrarPreautorizacionAsync(It.IsAny<clsPreautorizacionEdsSolicitud>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Simulada());

        await Handler().Handle(new clsPreautorizarEdsCommand(a.Id));

        _auditoria.Verify(x => x.RegistrarAsync(
            USUARIO, "preautorizar_eds", "autorizaciones_tanqueo", a.Id,
            It.Is<object?>(d => clsDatosPrueba.Json(d) == "{\"referencia\":\"SIMULADO-ABC123-1\"}"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Una_respuesta_no_ok_de_la_eds_tambien_se_guarda_sin_fingir_exito()
    {
        // Con Eds__ApiBaseUrl configurada y sin integracion real: ok = false, sin referencia.
        var a = Existente();
        _eds.Setup(e => e.RegistrarPreautorizacionAsync(It.IsAny<clsPreautorizacionEdsSolicitud>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new clsPreautorizacionEdsResultado(false, null, false, clsDatosPrueba.AHORA));

        var r = await Handler().Handle(new clsPreautorizarEdsCommand(a.Id));

        Assert.False(r.Eds.Ok);
        using var json = JsonDocument.Parse(a.EdsConfirmacion!);
        Assert.False(json.RootElement.GetProperty("ok").GetBoolean());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("referencia").ValueKind);
    }

    [Theory]
    [InlineData(EstadoAutorizacion.Propuesta)]
    [InlineData(EstadoAutorizacion.Ejecutada)]
    [InlineData(EstadoAutorizacion.Rechazada)]
    public async Task Exige_que_la_autorizacion_este_autorizada(EstadoAutorizacion estado)
    {
        var a = Existente(estado);

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsPreautorizarEdsCommand(a.Id)));

        Assert.Equal("TRANSICION_INVALIDA", ex.Code);
        _eds.Verify(e => e.RegistrarPreautorizacionAsync(It.IsAny<clsPreautorizacionEdsSolicitud>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Una_estacion_sin_soporte_de_preautorizacion_lanza_409()
    {
        var a = Existente();
        _estacion.SoportaPreautorizacion = false;

        var ex = await Assert.ThrowsAsync<clsConflictException>(() => Handler().Handle(new clsPreautorizarEdsCommand(a.Id)));

        Assert.Equal("ESTACION_SIN_PREAUTORIZACION", ex.Code);
        Assert.Null(a.EdsConfirmacion);
    }

    [Fact]
    public async Task Autorizacion_inexistente_lanza_404()
    {
        _autorizaciones.Setup(r => r.ObtenerAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((clsAutorizacionTanqueo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsPreautorizarEdsCommand(Guid.NewGuid())));

        Assert.Equal("AUTORIZACION_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Estacion_inexistente_lanza_404()
    {
        var a = Existente();
        _contexto.Setup(c => c.ObtenerEstacionAsync(_estacion.Id, It.IsAny<CancellationToken>())).ReturnsAsync((clsEstacion?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsPreautorizarEdsCommand(a.Id)));

        Assert.Equal("ESTACION_NO_ENCONTRADA", ex.Code);
    }

    [Fact]
    public async Task Vehiculo_inexistente_lanza_404()
    {
        var a = Existente();
        _contexto.Setup(c => c.ObtenerVehiculoAsync(_vehiculo.Id, It.IsAny<CancellationToken>())).ReturnsAsync((clsVehiculo?)null);

        var ex = await Assert.ThrowsAsync<clsNotFoundException>(() => Handler().Handle(new clsPreautorizarEdsCommand(a.Id)));

        Assert.Equal("VEHICULO_NO_ENCONTRADO", ex.Code);
    }
}
