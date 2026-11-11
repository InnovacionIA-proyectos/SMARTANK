namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Lo que devuelve un repositorio de escritura masiva: filas insertadas, actualizadas y las que fallaron (aisladas, sin abortar el resto).</summary>
public sealed record clsResultadoUpsertDto(int Insertados, int Actualizados, IReadOnlyList<clsErrorSincronizacion> Errores)
{
    public static clsResultadoUpsertDto Vacio { get; } = new(0, 0, []);
}
