using System.Text.Json;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Auditoria.Entidades;
using Smartank.Domain.Common;
using Smartank.Infrastructure.Persistence;

namespace Smartank.Infrastructure.Common;

/// <summary>
/// Escribe en tblAuditLog (append-only). Comparte el DbContext de la peticion: el registro se guarda junto con los
/// cambios pendientes en el mismo SaveChanges (misma transaccion), de modo que si un handler lo llama antes de
/// IUnitOfWork.SaveChangesAsync el cambio de estado y su auditoria son atomicos, y si lo llama despues tambien queda
/// persistido. El diff se serializa a JSON en snake_case, igual que el contrato del API.
/// </summary>
public sealed class clsEfAuditoriaRepository(clsSmartankDbContext db) : IAuditoriaPort
{
    private static readonly JsonSerializerOptions OPCIONES_JSON = CrearOpcionesJson();

    public async Task RegistrarAsync(
        Guid? actorId,
        string accion,
        string entidad,
        Guid? entidadId,
        object? diff = null,
        CancellationToken ct = default)
    {
        var ahora = DateTimeOffset.UtcNow;
        db.AuditLog.Add(new clsAuditLog
        {
            ActorId = actorId,
            Accion = accion,
            Entidad = entidad,
            EntidadId = entidadId,
            Diff = SerializarDiff(diff),
            // La tabla es inmutable: UpdatedAt = CreatedAt.
            CreatedAt = ahora,
            UpdatedAt = ahora,
        });
        await clsEfGuardado.GuardarAsync(db, ct);
    }

    /// <summary>
    /// La columna exige JSON de objeto o arreglo (CHECK ISJSON): un valor suelto (texto, numero) se envuelve en
    /// {"valor": ...}. Un diff null queda como NULL.
    /// </summary>
    internal static string? SerializarDiff(object? diff)
    {
        if (diff is null) return null;
        var json = JsonSerializer.Serialize(diff, OPCIONES_JSON);
        return json.StartsWith('{') || json.StartsWith('[') ? json : $"{{\"valor\":{json}}}";
    }

    private static JsonSerializerOptions CrearOpcionesJson()
    {
        var opciones = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
        opciones.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        return opciones;
    }
}
