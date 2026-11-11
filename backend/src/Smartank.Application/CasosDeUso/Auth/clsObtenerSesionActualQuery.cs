using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.CasosDeUso.Auth.Interfaces;
using Smartank.Application.CasosDeUso.Auth.Mappers;
using Smartank.Application.Common.Abstractions;
using Smartank.Application.Common.Exceptions;
using Smartank.Application.Common.Ports;

namespace Smartank.Application.CasosDeUso.Auth;

/// <summary>Usuario de la sesion actual; la SPA lo consulta al cargar para restaurar la sesion (GET /api/auth/me).</summary>
public sealed record clsObtenerSesionActualQuery : IQuery<clsUsuarioDto>;

public sealed class clsObtenerSesionActualQueryHandler(ICurrentUserPort usuarioActual, IUsuariosRepository usuarios)
    : IQueryHandler<clsObtenerSesionActualQuery, clsUsuarioDto>
{
    public async Task<clsUsuarioDto> Handle(clsObtenerSesionActualQuery query, CancellationToken ct = default)
    {
        if (usuarioActual.UsuarioId is not { } id)
            throw new clsUnauthorizedException("NO_AUTENTICADO", "No hay una sesión activa.");

        // Se relee de la base (no del token) para reflejar el rol vigente y rechazar cuentas ya eliminadas.
        var usuario = await usuarios.ObtenerPorIdAsync(id, ct)
            ?? throw new clsUnauthorizedException("SESION_INVALIDA", "La sesión ya no es válida. Inicie sesión de nuevo.");

        return clsUsuarioMapper.ADto(usuario);
    }
}
