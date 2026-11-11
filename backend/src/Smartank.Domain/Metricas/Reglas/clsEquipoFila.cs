namespace Smartank.Domain.Metricas.Reglas;

/// <summary>El tanqueo mas barato de un equipo (placa) en un mes: la tabla "mejor precio por equipo" del tablero.</summary>
public sealed record clsEquipoFila(
    string Equipo,
    int Mes,
    DateTimeOffset Fecha,
    decimal ValorGalon,
    decimal Galones,
    decimal ValorTotal,
    string Eds,
    string? Ciudad);
