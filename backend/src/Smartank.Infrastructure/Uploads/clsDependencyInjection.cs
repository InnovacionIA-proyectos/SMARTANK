using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Uploads.Interfaces;
using Smartank.Infrastructure.Uploads.Repositorios;

namespace Smartank.Infrastructure.Uploads;

public static class clsDependencyInjection
{
    /// <summary>Registra el almacenamiento de imagenes en la base. Requiere <c>AddPersistence</c>.</summary>
    public static IServiceCollection AddUploadsInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IUploadsRepository, clsEfUploadsRepository>();
        return services;
    }
}
