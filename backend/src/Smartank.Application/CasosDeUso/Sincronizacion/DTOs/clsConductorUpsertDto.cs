namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Conductor listo para escribir: el celular ya viene normalizado a +57... o null si no hay uno valido.</summary>
public sealed record clsConductorUpsertDto(
    long FpsapId, string Cedula, string Nombres, string? NumeroCelular, bool Activo, bool UtilizarTarjetaPrepago);
