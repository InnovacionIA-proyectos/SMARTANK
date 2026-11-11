using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Application.CasosDeUso.Alertas;

public static class clsDependencyInjection
{
    public static IServiceCollection AddAlertasUseCases(this IServiceCollection services)
    {
        services.AddScoped<IQueryHandler<clsListarAlertasQuery, clsAlertasResponse>, clsListarAlertasQueryHandler>();
        services.AddScoped<ICommandHandler<clsGestionarAlertaCommand, clsAlertaDto>, clsGestionarAlertaCommandHandler>();
        services.AddScoped<ICommandHandler<clsCrearAlertaCommand, clsAlertaDto>, clsCrearAlertaCommandHandler>();
        return services;
    }
}
