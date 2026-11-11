using Smartank.Domain.Enums;

namespace Smartank.Application.CasosDeUso.Auth.DTOs;

/// <summary>Usuario tal como lo ve la SPA. Nunca incluye el hash de la contrasena.</summary>
public record clsUsuarioDto(Guid Id, string Email, string? Nombre, Rol Rol);

/// <summary>Cuerpo de la respuesta de POST /api/auth/login: { usuario }. El token viaja solo en la cookie.</summary>
public record clsLoginResponse(clsUsuarioDto Usuario);

/// <summary>Token de sesion recien emitido y el instante en que vence.</summary>
public record clsSesionEmitida(string Token, DateTimeOffset ExpiraAt);

/// <summary>Resultado interno del login: el usuario y la sesion que el controller fija en la cookie.</summary>
public record clsLoginResultado(clsUsuarioDto Usuario, clsSesionEmitida Sesion);

/// <summary>Datos que un token de sesion valido afirma sobre su dueno.</summary>
public record clsSesionClaims(Guid UsuarioId, string Email, Rol Rol);

/// <summary>Resultado de crear el administrador inicial; <paramref name="Creado"/> es false si ya existia (idempotente).</summary>
public record clsCrearAdministradorResultado(bool Creado, string Email);
