using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;

namespace Smartank.Application.CasosDeUso.Autorizaciones.DTOs;

/// <summary>Resultado de pre-autorizar en la EDS: la autorizacion actualizada y la confirmacion recibida.</summary>
public sealed record clsPreautorizacionEdsDto(clsAutorizacionDto Autorizacion, clsPreautorizacionEdsResultado Eds);
