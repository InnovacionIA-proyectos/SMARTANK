using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Smartank.Application.CasosDeUso.Autorizaciones.Configuracion;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Infrastructure.Autorizaciones.Adaptadores;
using Smartank.Infrastructure.Autorizaciones.Repositorios;

namespace Smartank.Infrastructure.Autorizaciones;

public static class clsDependencyInjection
{
    /// <summary>Tiempo maximo de un intento de envio a WhatsApp (Evolution responde despues de enviar).</summary>
    private static readonly TimeSpan TIMEOUT_ENVIO_WHATSAPP = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Registra los repositorios de autorizaciones y de datos maestros del tanqueo, la URL publica de la app (<c>App__UrlPublica</c>),
    /// la API de la EDS (<c>Eds__*</c>) y el cliente de WhatsApp (<c>Whatsapp__*</c>) con timeout de 30 s y circuito de corte.
    /// Requiere <c>AddPersistence</c>.
    /// </summary>
    public static IServiceCollection AddAutorizacionesInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IAutorizacionesRepository, clsEfAutorizacionesRepository>();
        services.AddScoped<IContextoTanqueoRepository, clsEfContextoTanqueoRepository>();
        services.AddScoped<IAutorizacionesConsultas, clsEfAutorizacionesConsultas>();

        services.AddOptions<clsAplicacionOptions>().Bind(config.GetSection(clsAplicacionOptions.SECCION));

        services.AddOptions<clsEdsOptions>().Bind(config.GetSection(clsEdsOptions.SECCION));
        services.AddSingleton<IEdsPort, clsEdsAdapter>();

        services.AddOptions<clsWhatsappOptions>().Bind(config.GetSection(clsWhatsappOptions.SECCION));
        services.AddHttpClient<IWhatsappPort, clsEvolutionWhatsappAdapter>(cliente =>
                // Techo de seguridad por encima de la politica de resiliencia (que corta en 30 s por intento).
                cliente.Timeout = TIMEOUT_ENVIO_WHATSAPP + TimeSpan.FromSeconds(10))
            .AddStandardResilienceHandler(opciones =>
            {
                opciones.AttemptTimeout.Timeout = TIMEOUT_ENVIO_WHATSAPP;
                opciones.TotalRequestTimeout.Timeout = TIMEOUT_ENVIO_WHATSAPP + TimeSpan.FromSeconds(5);
                // Enviar un mensaje no es idempotente: un reintento tras un timeout podria duplicar la orden al conductor.
                opciones.Retry.DisableFor(HttpMethod.Post);
                // La muestra del circuito debe cubrir al menos el doble del timeout de un intento.
                opciones.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(120);
            });
        return services;
    }
}
