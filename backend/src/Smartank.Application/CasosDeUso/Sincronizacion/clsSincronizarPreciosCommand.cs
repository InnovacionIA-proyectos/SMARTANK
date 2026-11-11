using Microsoft.Extensions.Logging;
using Smartank.Application.CasosDeUso.Sincronizacion.DTOs;
using Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Ports;
using Smartank.Domain.Common;
using Smartank.Domain.Enums;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Sincroniza los precios reales de ACPM y corriente de las EDS de Terpel y Distracom desde las fuentes publicas de cada red.
/// Distracom cruza con el catalogo por codigo externo; Terpel por coordenadas cercanas (&lt;= 0.2 km) o nombre; la EDS
/// que no existe se crea. El ACPM negociado (Prodes) prevalece sobre el de los mapas. Si una fuente falla se registra y
/// se sigue con la otra. <paramref name="Auditar"/> deja constancia en la bitacora a nombre de <paramref name="ActorId"/>
/// (lo usa el endpoint; el Worker no audita). Idempotente.
/// </summary>
public sealed record clsSincronizarPreciosCommand(Guid? ActorId = null, bool Auditar = false) : ICommand<clsResultadoPreciosDto>;

public sealed class clsSincronizarPreciosCommandHandler(
    IPreciosEdsPort fuentes,
    IPreciosNegociadosPort negociados,
    IEstacionesSincronizacionRepository estaciones,
    IAuditoriaPort auditoria,
    ILogger<clsSincronizarPreciosCommandHandler> log)
    : ICommandHandler<clsSincronizarPreciosCommand, clsResultadoPreciosDto>
{
    public const string MODO_AUTOMATICO = "automatico";

    public async Task<clsResultadoPreciosDto> Handle(clsSincronizarPreciosCommand command, CancellationToken ct = default)
    {
        var errores = new List<string>();
        var tareaTerpel = LeerAsync("Terpel", () => fuentes.ObtenerTerpelAsync(ct), errores);
        var tareaDistracom = LeerAsync("Distracom", () => fuentes.ObtenerDistracomAsync(ct), errores);
        await Task.WhenAll(tareaTerpel, tareaDistracom);

        var precios = negociados.ObtenerAcpmPorNombre();
        var resultado = new Dictionary<string, clsResultadoFuentePreciosDto>();
        resultado[clsSnakeCase.ToSnake(FuenteTanqueo.Distracom)] =
            await ProcesarAsync(FuenteTanqueo.Distracom, tareaDistracom.Result, precios, errores, ct);
        resultado[clsSnakeCase.ToSnake(FuenteTanqueo.Terpel)] =
            await ProcesarAsync(FuenteTanqueo.Terpel, tareaTerpel.Result, precios, errores, ct);

        foreach (var (fuente, r) in resultado)
            log.LogInformation("Precios {Fuente}: {Total} de la fuente, {Actualizadas} actualizadas, {Creadas} creadas", fuente, r.Total, r.Actualizadas, r.Creadas);

        if (command.Auditar)
            await auditoria.RegistrarAsync(command.ActorId, "sync_precios_auto", "estaciones", null, resultado, ct);

        return new clsResultadoPreciosDto(MODO_AUTOMATICO, resultado, errores);
    }

    /// <summary>Lee una fuente aislando su fallo: una red caida no impide sincronizar la otra.</summary>
    private async Task<IReadOnlyList<clsPrecioEdsDto>> LeerAsync(
        string nombre, Func<Task<IReadOnlyList<clsPrecioEdsDto>>> leer, List<string> errores)
    {
        try
        {
            return await leer();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.LogError(ex, "Fallo al leer precios de {Fuente}", nombre);
            lock (errores) errores.Add($"{nombre}: {ex.Message}");
            return [];
        }
    }

    private async Task<clsResultadoFuentePreciosDto> ProcesarAsync(
        FuenteTanqueo proveedor,
        IReadOnlyList<clsPrecioEdsDto> fuente,
        IReadOnlyDictionary<string, decimal> negociadosPorNombre,
        List<string> errores,
        CancellationToken ct)
    {
        var nombreProveedor = clsSnakeCase.ToSnake(proveedor);
        var cruce = new clsCruceEstaciones(await estaciones.ListarPorProveedorAsync(nombreProveedor, ct));

        var actualizaciones = new List<clsActualizacionPrecioEstacionDto>();
        var nuevas = new List<clsEstacionNuevaDto>();
        foreach (var p in fuente)
        {
            var existente = cruce.Encontrar(p.CodigoExterno, p.Nombre, p.Lat, p.Lng);
            if (existente is not null)
            {
                var acpm = clsReglasPrecios.ResolverAcpm(negociadosPorNombre, existente.Nombre, p.Acpm);
                actualizaciones.Add(new clsActualizacionPrecioEstacionDto(existente.Id, acpm, p.Corriente));
            }
            else if (!string.IsNullOrWhiteSpace(p.Nombre))
            {
                // Una EDS nueva sin nombre no se puede crear (Nombre es obligatorio).
                var acpm = clsReglasPrecios.ResolverAcpm(negociadosPorNombre, p.Nombre, p.Acpm);
                nuevas.Add(new clsEstacionNuevaDto(
                    p.Nombre.Trim(), p.Lat, p.Lng, nombreProveedor, p.CodigoExterno, acpm, p.Corriente,
                    clsReglasPrecios.DescribirProducto(acpm, p.Corriente)));
            }
        }

        var actualizadas = await estaciones.ActualizarPreciosAsync(actualizaciones, ct);
        var creadas = await estaciones.InsertarAsync(nuevas, ct);
        foreach (var e in actualizadas.Errores.Concat(creadas.Errores)) errores.Add($"{nombreProveedor} {e.Clave}: {e.Mensaje}");

        return new clsResultadoFuentePreciosDto(fuente.Count, actualizadas.Actualizados, creadas.Insertados);
    }
}
