using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Smartank.Application.CasosDeUso.Sincronizacion;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.CasosDeUso.Sincronizacion.Orquestacion;
using Smartank.Infrastructure.Legacy;
using Smartank.Infrastructure.Sincronizacion.Adaptadores;
using Smartank.Infrastructure.Sincronizacion.Adaptadores.Geotab;
using Smartank.Infrastructure.Sincronizacion.Repositorios;

namespace Smartank.Infrastructure.Sincronizacion;

public static class clsDependencyInjection
{
    private const int REINTENTOS_HTTP = 2;

    /// <summary>
    /// Registra lo que necesitan las sincronizaciones: opciones <c>Sync</c>, <c>Geotab</c> y <c>Precios</c>; adaptadores del SQL
    /// Server legado (solo lectura), cliente de Geotab (HttpClient tipado con resiliencia), fuentes de precios, repositorios de
    /// escritura masiva y el bloqueo distribuido. Ninguna cadena ni credencial es obligatoria al registrar: si falta una, el
    /// puerto afectado lanza <c>*_NO_CONFIGURADO</c> al usarse y el arranque no se cae.
    /// </summary>
    public static IServiceCollection AddSincronizacionInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<clsSyncOptions>().Bind(config.GetSection(clsSyncOptions.SECCION));
        services.AddOptions<clsGeotabOptions>().Bind(config.GetSection(clsGeotabOptions.SECCION));
        services.AddOptions<clsPreciosOptions>().Bind(config.GetSection(clsPreciosOptions.SECCION));

        // SQL Server legado (solo lectura): capa anticorrupcion.
        services.AddSingleton<clsLegadoConexion>();
        services.AddScoped<ICabezotesLegadoPort, clsCabezotesLegadoAdapter>();
        services.AddScoped<IConductoresLegadoPort, clsConductoresLegadoAdapter>();
        services.AddScoped<IFletesLegadoPort, clsFletesLegadoAdapter>();
        services.AddScoped<IManifiestosCerradosPort, clsManifiestosCerradosLegadoAdapter>();
        services.AddScoped<ITanqueosLegadoPort, clsTanqueosLegadoAdapter>();
        services.AddScoped<IRutasLegadoPort, clsRutasLegadoAdapter>();

        // Base propia: escrituras masivas con SqlBulkCopy (no usan el DbContext).
        services.AddSingleton<clsConexionSmartank>();
        services.AddScoped<IVehiculosSincronizacionRepository, clsSqlVehiculosSincronizacionRepository>();
        services.AddScoped<IConductoresSincronizacionRepository, clsSqlConductoresSincronizacionRepository>();
        services.AddScoped<IViajesSincronizacionRepository, clsSqlViajesSincronizacionRepository>();
        services.AddScoped<IRutasSincronizacionRepository, clsSqlRutasSincronizacionRepository>();
        services.AddScoped<ITanqueosSincronizacionRepository, clsSqlTanqueosSincronizacionRepository>();
        services.AddScoped<IEstacionesSincronizacionRepository, clsSqlEstacionesSincronizacionRepository>();
        services.AddScoped<IPreciosRutaRepository, clsSqlPreciosRutaRepository>();
        services.AddSingleton<IBloqueoDistribuidoPort, clsBloqueoDistribuidoSqlServer>();

        // Geotab: sesion compartida por el proceso + HttpClient tipado con reintentos y timeout.
        services.AddSingleton<clsGeotabSesionCache>();
        var geotabTimeout = SegundosValidos(config[$"{clsGeotabOptions.SECCION}:{nameof(clsGeotabOptions.TimeoutSegundos)}"], clsGeotabOptions.TIMEOUT_POR_DEFECTO_SEGUNDOS);
        services.AddHttpClient<IGeotabPort, clsGeotabClient>(c => c.Timeout = Timeout.InfiniteTimeSpan)   // el handler de resiliencia pone los timeouts
            .AddStandardResilienceHandler(o => ConfigurarResiliencia(o, geotabTimeout));

        // Precios de EDS: fuentes publicas de Terpel y Distracom. Sin cookies automaticas: Distracom las maneja a mano.
        services.AddSingleton<IPreciosNegociadosPort, clsPreciosNegociadosAdapter>();
        var preciosTimeout = SegundosValidos(config[$"{clsPreciosOptions.SECCION}:{nameof(clsPreciosOptions.TimeoutSegundos)}"], clsPreciosOptions.TIMEOUT_POR_DEFECTO_SEGUNDOS);
        services.AddHttpClient<IPreciosEdsPort, clsPreciosHttpClient>(c => c.Timeout = Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                UseCookies = false,
                AutomaticDecompression = System.Net.DecompressionMethods.All,
            })
            .AddStandardResilienceHandler(o => ConfigurarResiliencia(o, preciosTimeout));

        return services;
    }

    /// <summary>
    /// Reintentos y timeouts coherentes con el intento de <paramref name="segundosPorIntento"/>: el timeout total cubre todos los
    /// intentos y la ventana de muestreo del cortacircuitos debe ser al menos el doble del timeout por intento (validacion de Polly).
    /// </summary>
    private static void ConfigurarResiliencia(HttpStandardResilienceOptions o, int segundosPorIntento)
    {
        var intento = TimeSpan.FromSeconds(segundosPorIntento);
        o.Retry.MaxRetryAttempts = REINTENTOS_HTTP;
        o.Retry.UseJitter = true;
        o.Retry.Delay = TimeSpan.FromSeconds(2);
        o.AttemptTimeout.Timeout = intento;
        o.TotalRequestTimeout.Timeout = intento * (REINTENTOS_HTTP + 1) + TimeSpan.FromSeconds(30);
        o.CircuitBreaker.SamplingDuration = intento * 3;
    }

    private static int SegundosValidos(string? valor, int porDefecto) =>
        int.TryParse(valor, out var segundos) && segundos is >= 5 and <= 600 ? segundos : porDefecto;
}
