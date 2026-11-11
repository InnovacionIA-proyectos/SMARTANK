using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Infrastructure.Bitacora.Adaptadores;
using Smartank.Infrastructure.Bitacora.Repositorios;

namespace Smartank.Infrastructure.Bitacora;

public static class clsDependencyInjection
{
    /// <summary>
    /// Registra el token firmado de la bitacora (<c>Bitacora__Secret</c>, <c>Bitacora__ExpiraHoras</c>) y la consulta de la
    /// orden. Un secreto ausente NO tumba el arranque: el primer uso responde 503 BITACORA_SECRET_NO_CONFIGURADO (el host puede
    /// exigirlo al arrancar con <see cref="clsBitacoraOptions.SecretoValido"/>). Requiere <c>AddPersistence</c>.
    /// </summary>
    public static IServiceCollection AddBitacoraInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<clsBitacoraOptions>().Bind(config.GetSection(clsBitacoraOptions.SECCION));
        services.AddSingleton<IBitacoraTokenPort, clsJwtBitacoraTokenAdapter>();
        services.AddScoped<IBitacoraConsultas, clsEfBitacoraConsultas>();
        return services;
    }
}
