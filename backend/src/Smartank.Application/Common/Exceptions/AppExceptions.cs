namespace Smartank.Application.Common.Exceptions;

/// <summary>
/// Excepcion de un caso de uso. Application no conoce HTTP: no lleva StatusCode.
/// Es el middleware de Api quien traduce el TIPO de excepcion a un codigo HTTP.
/// </summary>
public abstract class clsAppException : Exception
{
    public string Code { get; }
    public object? Details { get; }

    protected clsAppException(string code, string message, object? details = null) : base(message)
    {
        Code = code;
        Details = details;
    }
}

// No son sealed a proposito: el middleware de Api traduce por TIPO, asi que las excepciones de un contexto
// deben heredar de una de estas para conservar su codigo HTTP (heredar de clsAppException daria 500).

/// <summary>El recurso no existe (Api la traduce a 404).</summary>
public class clsNotFoundException(string code, string message) : clsAppException(code, message);

/// <summary>Input invalido por una regla de negocio (Api la traduce a 400).</summary>
public class clsValidationFailedException(string code, string message, object? details = null)
    : clsAppException(code, message, details);

/// <summary>Conflicto con el estado actual del recurso (Api la traduce a 409).</summary>
public class clsConflictException(string code, string message) : clsAppException(code, message);

/// <summary>Una dependencia externa no esta configurada o no responde (Api la traduce a 503).</summary>
public class clsServiceUnavailableException(string code, string message) : clsAppException(code, message);

/// <summary>Se supero un tope de uso configurado; reintentar despues si funciona (Api la traduce a 429).</summary>
public class clsLimiteExcedidoException(string code, string message, object? details = null) : clsAppException(code, message, details);

/// <summary>El usuario autenticado no tiene permiso sobre el recurso (Api la traduce a 403).</summary>
public class clsForbiddenException(string code, string message) : clsAppException(code, message);

/// <summary>Credenciales o token ausentes/invalidos (Api la traduce a 401).</summary>
public class clsUnauthorizedException(string code, string message) : clsAppException(code, message);
