using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Smartank.Api.Common.Auth;
using Smartank.Api.Common.RateLimiting;
using Smartank.Application.CasosDeUso.Auth;
using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Application.Common.Abstractions;

namespace Smartank.Api.Auth;

/// <summary>Inicio y cierre de sesion. El token viaja solo en una cookie httpOnly; el cuerpo nunca lo incluye.</summary>
[ApiController]
[Route("api/auth")]
public class clsAuthController(
    ICommandHandler<clsLoginCommand, clsLoginResultado> login,
    IQueryHandler<clsObtenerSesionActualQuery, clsUsuarioDto> sesionActual,
    IOptions<clsCookieOptions> cookie)
    : ControllerBase
{
    /// <summary>Inicia sesion con correo y contraseña y fija la cookie de sesion.</summary>
    /// <remarks>Responde igual (401 CREDENCIALES_INVALIDAS) si el correo no existe o la contraseña es incorrecta.</remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaLogin)]
    [ProducesResponseType(typeof(clsLoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<clsLoginResponse>> Login([FromBody] clsLoginRequest req, CancellationToken ct = default)
    {
        var resultado = await login.Handle(new clsLoginCommand(req.Email, req.Password), ct);
        clsCookieSesion.Escribir(Response, resultado.Sesion.Token, resultado.Sesion.ExpiraAt, cookie.Value.Secure);
        return Ok(new clsLoginResponse(resultado.Usuario));
    }

    /// <summary>Cierra la sesion borrando la cookie. No exige sesion: debe poder limpiarse una cookie ya vencida.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        clsCookieSesion.Borrar(Response, cookie.Value.Secure);
        return Ok(new { ok = true });
    }

    /// <summary>Usuario de la sesion actual; la SPA lo consulta al cargar para restaurar la sesion.</summary>
    [HttpGet("me")]
    [Authorize]
    [EnableRateLimiting(clsRateLimitingExtensions.PoliticaListadosEstandar)]
    [ProducesResponseType(typeof(clsUsuarioDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<clsUsuarioDto>> Me(CancellationToken ct = default)
        => Ok(await sesionActual.Handle(new clsObtenerSesionActualQuery(), ct));
}

/// <summary>Cuerpo de POST /api/auth/login.</summary>
public sealed record clsLoginRequest(string? Email, string? Password);
