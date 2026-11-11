using System.Text;

namespace Smartank.Domain.Common;

/// <summary>Conversion PascalCase &lt;-&gt; snake_case para enums (texto en JSON y en la base de datos).</summary>
public static class clsSnakeCase
{
    /// <summary>"EjecutadaParcial" -> "ejecutada_parcial".</summary>
    public static string ToSnake(string valor)
    {
        if (string.IsNullOrEmpty(valor)) return valor;
        var sb = new StringBuilder(valor.Length + 4);
        for (var i = 0; i < valor.Length; i++)
        {
            var c = valor[i];
            if (char.IsUpper(c))
            {
                if (i > 0 && (char.IsLower(valor[i - 1]) || char.IsDigit(valor[i - 1]))) sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    /// <summary>Texto snake_case de un miembro de enum.</summary>
    public static string ToSnake<TEnum>(TEnum valor) where TEnum : struct, Enum => ToSnake(valor.ToString());

    /// <summary>"ejecutada_parcial" -> EjecutadaParcial. Lanza <see cref="ArgumentException"/> si el texto no corresponde a ningun miembro.</summary>
    public static TEnum Parse<TEnum>(string texto) where TEnum : struct, Enum
    {
        foreach (var miembro in Enum.GetValues<TEnum>())
            if (string.Equals(ToSnake(miembro), texto, StringComparison.Ordinal)) return miembro;
        throw new ArgumentException($"'{texto}' no es un valor valido de {typeof(TEnum).Name}.", nameof(texto));
    }

    /// <summary>Variante que devuelve false en vez de lanzar.</summary>
    public static bool TryParse<TEnum>(string? texto, out TEnum valor) where TEnum : struct, Enum
    {
        if (texto is not null)
            foreach (var miembro in Enum.GetValues<TEnum>())
                if (string.Equals(ToSnake(miembro), texto, StringComparison.Ordinal))
                {
                    valor = miembro;
                    return true;
                }
        valor = default;
        return false;
    }
}
