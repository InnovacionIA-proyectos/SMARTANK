using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Ports;
using Smartank.Infrastructure.Common;

namespace Smartank.Infrastructure.Persistence;

public static class clsPersistenceDependencyInjection
{
    /// <summary>Nivel de compatibilidad de SQL Server 2019: evita que EF genere SQL de versiones mas nuevas.</summary>
    private const int NIVEL_COMPATIBILIDAD_SQL_SERVER_2019 = 150;

    /// <summary>
    /// Registra el DbContext de la base propia (cadena <c>ConnectionStrings__Smartank</c>), la unidad de trabajo y la
    /// auditoria (todos scoped). Con EnableRetryOnFailure, quien abra una transaccion manual debe hacerlo dentro de
    /// <c>db.Database.CreateExecutionStrategy().ExecuteAsync(...)</c>.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration config)
    {
        // La cadena se valida al construir el contexto (no al registrar) para que la aplicacion arranque y /health responda
        // aunque falte la configuracion; el primer acceso a datos falla con un mensaje claro.
        services.AddDbContext<clsSmartankDbContext>(options =>
        {
            var cadena = config.GetConnectionString("Smartank");
            if (string.IsNullOrWhiteSpace(cadena))
                throw new InvalidOperationException(
                    "Falta la cadena de conexion de la base propia (ConnectionStrings__Smartank).");

            options.UseSqlServer(cadena, sql =>
            {
                sql.EnableRetryOnFailure();
                sql.UseCompatibilityLevel(NIVEL_COMPATIBILIDAD_SQL_SERVER_2019);
            });
        });

        services.AddScoped<IUnitOfWork, clsEfUnitOfWork>();
        services.AddScoped<IAuditoriaPort, clsEfAuditoriaRepository>();
        return services;
    }
}
