using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Smartank.Application.Common.Exceptions;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Common;

/// <summary>
/// Guarda los cambios traduciendo los errores de concurrencia y de unicidad de la base a excepciones de caso de uso
/// (Application no conoce EF Core ni SqlClient).
/// </summary>
internal static class clsEfGuardado
{
    // Numeros de error de SQL Server: violacion de indice unico (2601) y de restriccion UNIQUE (2627).
    private const int ERROR_INDICE_UNICO = 2601;
    private const int ERROR_RESTRICCION_UNICA = 2627;

    public static async Task<int> GuardarAsync(clsSmartankDbContext db, CancellationToken ct)
    {
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Otro usuario cambio la fila (rowversion distinto) o ya no existe: el cliente debe recargar y reintentar.
            throw new clsConflictException(
                "CONFLICTO_CONCURRENCIA",
                "El registro fue modificado por otra operacion. Recargue e intente de nuevo.");
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: ERROR_INDICE_UNICO or ERROR_RESTRICCION_UNICA })
        {
            throw new clsConflictException(
                "REGISTRO_DUPLICADO",
                "Ya existe un registro con los mismos datos unicos.");
        }
    }
}
