using Smartank.Application.CasosDeUso.Bitacora.DTOs;
using Smartank.Application.CasosDeUso.Bitacora.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Bitacora;

/// <summary>Datos de la orden de tanqueo a partir del token del enlace de WhatsApp (sin sesion: el token es la credencial).</summary>
public sealed record clsObtenerBitacoraQuery(string Token) : IQuery<clsBitacoraDto>;

public sealed class clsObtenerBitacoraQueryHandler(IBitacoraTokenPort tokens, IBitacoraConsultas consultas)
    : IQueryHandler<clsObtenerBitacoraQuery, clsBitacoraDto>
{
    public async Task<clsBitacoraDto> Handle(clsObtenerBitacoraQuery query, CancellationToken ct = default)
    {
        var autorizacionId = tokens.Validar(query.Token);
        return await consultas.ObtenerAsync(autorizacionId, ct)
            ?? throw new clsNotFoundException("AUTORIZACION_NO_ENCONTRADA", "La autorización del enlace no existe.");
    }
}
