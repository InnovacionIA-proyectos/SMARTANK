using Microsoft.EntityFrameworkCore;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Api.Tests.Infra;

/// <summary>Crea DbContexts contra la base indicada en <c>TEST_DB_CONNECTION</c> (sin reintentos, para ver el error real).</summary>
public static class clsContextoDePruebas
{
    public static clsSmartankDbContext Crear()
    {
        var cadena = clsHechoConBaseDePruebasAttribute.CadenaDeConexion
            ?? throw new InvalidOperationException($"Falta {clsHechoConBaseDePruebasAttribute.VARIABLE_ENTORNO}.");
        var opciones = new DbContextOptionsBuilder<clsSmartankDbContext>()
            .UseSqlServer(cadena, sql => sql.UseCompatibilityLevel(150))
            .Options;
        return new clsSmartankDbContext(opciones);
    }
}
