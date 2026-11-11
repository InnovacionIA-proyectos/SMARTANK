namespace Smartank.Infrastructure.Auth;

/// <summary>Parametros de seguridad de las contrasenas: variable Seguridad__BcryptRounds.</summary>
public class clsSeguridadOptions
{
    public const string SECCION = "Seguridad";

    public const int COSTO_MINIMO = 4;
    public const int COSTO_MAXIMO = 31;

    /// <summary>Costo (log2 de las rondas) de bcrypt para hashes nuevos; 12 equivale a ~250 ms por hash. No afecta la verificacion de hashes ya creados.</summary>
    public int BcryptRounds { get; set; } = 12;
}
