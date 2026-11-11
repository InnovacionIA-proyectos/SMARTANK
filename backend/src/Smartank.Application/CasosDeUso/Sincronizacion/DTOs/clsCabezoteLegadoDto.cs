namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Cabezote (vehiculo) del maestro legado tblCabezotes, ya traducido a nombres propios. Solo los activos se sincronizan.</summary>
public sealed record clsCabezoteLegadoDto(
    long FpsapId,
    string Placa,
    string TipoMotor,
    decimal? CapacidadTanqueGal,
    decimal? GalonesReserva,
    decimal? GalonesUtilizables,
    decimal? GalonesTolerancia,
    string? ChipTanqueo,
    string? NitPropietario,
    string? AliasPlaca,
    bool Activo);
