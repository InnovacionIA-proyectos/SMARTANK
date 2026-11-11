using Smartank.Application.CasosDeUso.Uploads.DTOs;
using Smartank.Application.CasosDeUso.Uploads.Interfaces;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Uploads;

/// <summary>Imagen almacenada (foto del surtidor). Solo para usuarios con sesion.</summary>
public sealed record clsObtenerImagenQuery(Guid Id) : IQuery<clsImagenDto>;

public sealed class clsObtenerImagenQueryHandler(IUploadsRepository uploads)
    : IQueryHandler<clsObtenerImagenQuery, clsImagenDto>
{
    public async Task<clsImagenDto> Handle(clsObtenerImagenQuery query, CancellationToken ct = default)
    {
        var upload = await uploads.ObtenerAsync(query.Id, ct)
            ?? throw new clsNotFoundException("IMAGEN_NO_ENCONTRADA", "Imagen no encontrada.");
        return new clsImagenDto(upload.ContentType, upload.Data);
    }
}
