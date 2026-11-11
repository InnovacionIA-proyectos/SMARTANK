namespace Smartank.Application.CasosDeUso.Sincronizacion.DTOs;

/// <summary>Tanqueo listo para escribir (clave Fuente + FuenteId). VehiculoId es null si la placa no esta en la flota: el registro no se descarta.</summary>
public sealed record clsTanqueoUpsertDto(clsTanqueoLegadoDto Origen, Guid? VehiculoId);
