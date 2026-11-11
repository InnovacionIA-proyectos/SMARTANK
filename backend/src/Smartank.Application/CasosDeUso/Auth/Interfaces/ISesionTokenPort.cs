using Smartank.Application.CasosDeUso.Auth.DTOs;

namespace Smartank.Application.CasosDeUso.Auth.Interfaces;

/// <summary>Emision y validacion del token de sesion (JWT HS256 que viaja en la cookie httpOnly). Implementado en Infrastructure.</summary>
public interface ISesionTokenPort
{
    clsSesionEmitida Emitir(clsUsuarioDto usuario);

    /// <summary>Claims del token si la firma, el emisor y la vigencia son validos; null en cualquier otro caso.</summary>
    clsSesionClaims? Validar(string token);
}
