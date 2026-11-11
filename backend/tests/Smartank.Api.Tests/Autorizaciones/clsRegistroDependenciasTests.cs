using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Smartank.Application.CasosDeUso.Autonomia;
using Smartank.Application.CasosDeUso.Autonomia.DTOs;
using Smartank.Application.CasosDeUso.Autorizaciones;
using Smartank.Application.CasosDeUso.Autorizaciones.DTOs;
using Smartank.Application.CasosDeUso.Bitacora;
using Smartank.Application.CasosDeUso.Bitacora.DTOs;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.CasosDeUso.Eventos;
using Smartank.Application.CasosDeUso.Eventos.DTOs;
using Smartank.Application.CasosDeUso.Recomendacion;
using Smartank.Application.CasosDeUso.Recomendacion.DTOs;
using Smartank.Application.CasosDeUso.Uploads;
using Smartank.Application.CasosDeUso.Uploads.DTOs;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Ports;
using Smartank.Infrastructure.Autorizaciones;
using Smartank.Infrastructure.Bitacora;
using Smartank.Infrastructure.Eventos;
using Smartank.Infrastructure.Persistence;
using Smartank.Infrastructure.Recomendacion;
using Smartank.Infrastructure.Uploads;

namespace Smartank.Api.Tests.Autorizaciones;

/// <summary>Comprueba que el cableado DI del modulo (casos de uso + infraestructura) resuelve completo y con scopes validos.</summary>
public class clsRegistroDependenciasTests
{
    private static ServiceProvider Construir()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Smartank"] = "Server=127.0.0.1,1;Database=x;User Id=x;Password=x;TrustServerCertificate=True",
            ["App:UrlPublica"] = "https://smartank.test",
            ["Bitacora:Secret"] = new string('s', 40),
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddPersistence(config);
        services.AddSingleton(Mock.Of<ICurrentUserPort>());
        services.AddAutonomiaUseCases();
        services.AddRecomendacionUseCases();
        services.AddAutorizacionesUseCases();
        services.AddBitacoraUseCases();
        services.AddEventosUseCases();
        services.AddUploadsUseCases();
        services.AddAutorizacionesInfrastructure(config);
        services.AddRecomendacionInfrastructure(config);
        services.AddBitacoraInfrastructure(config);
        services.AddEventosInfrastructure(config);
        services.AddUploadsInfrastructure(config);
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
    }

    [Fact]
    public void Todos_los_handlers_del_modulo_se_resuelven()
    {
        using var raiz = Construir();
        using var scope = raiz.CreateScope();
        var sp = scope.ServiceProvider;

        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsCalcularAutonomiaCommand, clsAutonomiaDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsRecomendarTanqueoCommand, clsRecomendacionDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsCrearAutorizacionCommand, clsAutorizacionDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsAutorizarAutorizacionCommand, clsAutorizacionDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsPreautorizarEdsCommand, clsPreautorizacionEdsDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsNotificarAutorizacionCommand, clsNotificarDto>>());
        Assert.NotNull(sp.GetRequiredService<IQueryHandler<clsObtenerAutorizacionDetalleQuery, clsAutorizacionDetalleDto>>());
        Assert.NotNull(sp.GetRequiredService<IQueryHandler<clsListarAutorizacionesQuery, clsAutorizacionesListaDto>>());
        Assert.NotNull(sp.GetRequiredService<IQueryHandler<clsObtenerBitacoraQuery, clsBitacoraDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsRegistrarEventoCommand, clsEventoDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsVerificarEventoCommand, clsVerificacionDto>>());
        Assert.NotNull(sp.GetRequiredService<IQueryHandler<clsListarHistorialTanqueosQuery, clsHistorialTanqueosDto>>());
        Assert.NotNull(sp.GetRequiredService<ICommandHandler<clsSubirImagenCommand, clsImagenSubidaDto>>());
        Assert.NotNull(sp.GetRequiredService<IQueryHandler<clsObtenerImagenQuery, clsImagenDto>>());
    }

    [Fact]
    public void El_token_de_la_bitacora_emite_con_la_configuracion_registrada()
    {
        using var raiz = Construir();
        var tokens = raiz.GetRequiredService<IBitacoraTokenPort>();
        var id = Guid.NewGuid();

        Assert.Equal(id, tokens.Validar(tokens.Emitir(id)));
    }
}
