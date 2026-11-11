using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Application.CasosDeUso.Auth.Mappers;

public static class clsUsuarioMapper
{
    public static clsUsuarioDto ADto(clsUsuario usuario) => new(usuario.Id, usuario.Email, usuario.Nombre, usuario.Rol);
}
