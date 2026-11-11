using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Eventos.Interfaces;
using Smartank.Infrastructure.Eventos.Repositorios;

namespace Smartank.Infrastructure.Eventos;

public static class clsDependencyInjection
{
    /// <summary>Registra los repositorios de eventos de bitacora, alertas del evento e historial de tanqueos. Requiere <c>AddPersistence</c>.</summary>
    public static IServiceCollection AddEventosInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IEventosRepository, clsEfEventosRepository>();
        services.AddScoped<IEventosConsultas, clsEfEventosConsultas>();
        return services;
    }
}
