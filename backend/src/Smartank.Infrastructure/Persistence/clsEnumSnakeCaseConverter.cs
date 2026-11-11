using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Smartank.Domain.Common;

namespace Smartank.Infrastructure.Persistence;

/// <summary>
/// Guarda un enum como texto snake_case (EjecutadaParcial -> "ejecutada_parcial"), igual que el contrato JSON.
/// Los CHECK de ddl.sql validan exactamente esos valores.
/// </summary>
public sealed class clsEnumSnakeCaseConverter<TEnum>()
    : ValueConverter<TEnum, string>(v => clsSnakeCase.ToSnake(v), v => clsSnakeCase.Parse<TEnum>(v))
    where TEnum : struct, Enum;
