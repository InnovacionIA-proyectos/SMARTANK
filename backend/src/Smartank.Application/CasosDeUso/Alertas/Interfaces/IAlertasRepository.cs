using Smartank.Application.CasosDeUso.Alertas.DTOs;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Alertas.Interfaces;

public interface IAlertasRepository
{
    /// <summary>Bandeja de alertas con la placa del vehiculo, de la mas reciente a la mas antigua; con <paramref name="estado"/> solo las de ese estado.</summary>
    Task<IReadOnlyList<clsAlertaListadoDto>> ListarAsync(EstadoAlerta? estado, CancellationToken ct);

    /// <summary>
    /// Alerta CON seguimiento de cambios, para modificarla. Se guarda con su rowversion: si otro usuario la cambio entre la
    /// lectura y el guardado, el guardado falla con 409 CONFLICTO_CONCURRENCIA en vez de pisar su cambio.
    /// </summary>
    Task<clsAlerta?> ObtenerParaActualizarAsync(Guid id, CancellationToken ct);

    void Agregar(clsAlerta alerta);

    Task<bool> ExisteVehiculoAsync(Guid vehiculoId, CancellationToken ct);

    Task<bool> ExisteEventoAsync(Guid eventoId, CancellationToken ct);
}
