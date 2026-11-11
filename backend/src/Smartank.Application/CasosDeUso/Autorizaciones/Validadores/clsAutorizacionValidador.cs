using Smartank.Application.Common.Exceptions;

namespace Smartank.Application.CasosDeUso.Autorizaciones.Validadores;

/// <summary>Forma de una autorizacion creada a mano. Que el viaje, el vehiculo y la estacion existan lo comprueba el caso de uso (consulta datos).</summary>
public static class clsAutorizacionValidador
{
    public static void Validar(
        Guid? viajeId, Guid? vehiculoId, Guid? estacionId,
        decimal? galonesMax, decimal? precioTecho,
        DateTimeOffset? fechaInicio, DateTimeOffset? fechaFin)
    {
        if (viajeId is null || viajeId == Guid.Empty)
            throw Invalida("Falta el viaje de la autorización.");
        if (vehiculoId is null || vehiculoId == Guid.Empty)
            throw Invalida("Falta el vehículo de la autorización.");
        if (estacionId is null || estacionId == Guid.Empty)
            throw Invalida("Falta la estación de la autorización.");
        if (galonesMax is null or <= 0)
            throw Invalida("Los galones máximos deben ser mayores que cero.");
        if (precioTecho is null or < 0)
            throw Invalida("El precio techo no puede ser negativo.");
        if (fechaInicio is null || fechaFin is null)
            throw Invalida("Faltan las fechas de inicio y fin de la ventana.");
        if (fechaFin < fechaInicio)
            throw Invalida("La fecha de fin debe ser posterior o igual a la de inicio.");
    }

    private static clsValidationFailedException Invalida(string mensaje) => new("AUTORIZACION_INVALIDA", mensaje);
}
