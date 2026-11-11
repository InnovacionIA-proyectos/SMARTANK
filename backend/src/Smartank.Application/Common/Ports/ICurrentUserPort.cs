using Smartank.Domain.Enums;

namespace Smartank.Application.Common.Ports;

/// <summary>Usuario autenticado de la peticion actual (lo implementa Api a partir de la sesion).</summary>
public interface ICurrentUserPort
{
    bool EstaAutenticado { get; }

    /// <summary>Id del usuario (claim sub). Null si no hay sesion.</summary>
    Guid? UsuarioId { get; }

    string? Email { get; }

    Rol? Rol { get; }
}
