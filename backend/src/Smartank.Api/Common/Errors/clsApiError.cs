namespace Smartank.Api.Common.Errors;

/// <summary>Forma estandar de toda respuesta de error del API: { code, message, timestamp, details? }.</summary>
public record clsApiError(string Code, string Message, DateTimeOffset Timestamp, object? Details = null);
