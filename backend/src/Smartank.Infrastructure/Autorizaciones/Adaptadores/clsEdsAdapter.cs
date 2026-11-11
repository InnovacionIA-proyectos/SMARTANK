using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

namespace Smartank.Infrastructure.Autorizaciones.Adaptadores;

/// <summary>
/// Pre-autorizacion de carga en la EDS. Conserva el comportamiento del origen: sin Eds__ApiBaseUrl devuelve una confirmacion
/// SIMULADA (marcada) para no bloquear el flujo; con URL configurada pero sin integracion real todavia, responde ok = false
/// (no se finge una confirmacion que la EDS no dio).
/// </summary>
public sealed class clsEdsAdapter(IOptions<clsEdsOptions> options, TimeProvider reloj) : IEdsPort
{
    public Task<clsPreautorizacionEdsResultado> RegistrarPreautorizacionAsync(
        clsPreautorizacionEdsSolicitud solicitud, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();

        if (string.IsNullOrWhiteSpace(options.Value.ApiBaseUrl))
        {
            // Modo simulado (desarrollo o sin EDS piloto): se marca claramente como tal.
            return Task.FromResult(new clsPreautorizacionEdsResultado(
                Ok: true,
                Referencia: $"SIMULADO-{solicitud.Placa}-{ahora.ToUnixTimeMilliseconds()}",
                Simulado: true,
                RecibidoAt: ahora));
        }

        // Pendiente: el POST real a Eds__ApiBaseUrl (con Eds__ApiKey) depende del contrato con la EDS piloto.
        return Task.FromResult(new clsPreautorizacionEdsResultado(Ok: false, Referencia: null, Simulado: false, RecibidoAt: ahora));
    }
}
