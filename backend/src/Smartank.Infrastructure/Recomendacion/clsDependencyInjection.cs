using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.CasosDeUso.Recomendacion.Interfaces;
using Smartank.Infrastructure.Recomendacion.Repositorios;

namespace Smartank.Infrastructure.Recomendacion;

public static class clsDependencyInjection
{
    /// <summary>Registra las lecturas de la recomendacion (estaciones y polilinea). Requiere <c>AddPersistence</c>.</summary>
    public static IServiceCollection AddRecomendacionInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<IRecomendacionRepository, clsEfRecomendacionRepository>();
        return services;
    }
}
