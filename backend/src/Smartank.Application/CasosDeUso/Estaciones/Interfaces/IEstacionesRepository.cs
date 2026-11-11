using Smartank.Domain.Estaciones.Entidades;

namespace Smartank.Application.CasosDeUso.Estaciones.Interfaces;

public interface IEstacionesRepository
{
    /// <summary>
    /// Estaciones del catalogo sin seguimiento de cambios. Con <paramref name="ruta"/> solo las de esa ruta, de la mas
    /// barata a la mas cara; sin ella, todas agrupadas por ruta y luego por precio. Las que no tienen precio van al final.
    /// </summary>
    Task<IReadOnlyList<clsEstacion>> ListarAsync(string? ruta, CancellationToken ct);
}
