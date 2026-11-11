namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Conductor del maestro legado tblConductores.</summary>
public sealed record clsConductorLegadoDto(long FpsapId, string Cedula, string Nombres, bool Activo, bool UtilizarTarjetaPrepago);
