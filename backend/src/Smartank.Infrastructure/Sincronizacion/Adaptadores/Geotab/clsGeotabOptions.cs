namespace Smartank.Infrastructure.Sincronizacion.Adaptadores.Geotab;

/// <summary>Configuracion del cliente de Geotab (seccion <c>Geotab</c>; variables <c>Geotab__*</c>).</summary>
public class clsGeotabOptions
{
    public const string SECCION = "Geotab";

    public const string SERVIDOR_POR_DEFECTO = "my.geotab.com";

    /// <summary>Segundos de espera por intento. Las consultas de StatusData de ventanas largas (rendimiento) pueden requerir hasta 120.</summary>
    public const int TIMEOUT_POR_DEFECTO_SEGUNDOS = 60;

    /// <summary>Base de datos de Geotab (MyGeotab).</summary>
    public string? Database { get; set; }

    public string? Username { get; set; }

    /// <summary>Secreto: llega solo por variable de entorno.</summary>
    public string? Password { get; set; }

    /// <summary>Punto de entrada; la autenticacion puede redirigir a otro host de la federacion (se sigue el <c>path</c> devuelto).</summary>
    public string Server { get; set; } = SERVIDOR_POR_DEFECTO;

    public int TimeoutSegundos { get; set; } = TIMEOUT_POR_DEFECTO_SEGUNDOS;

    public bool Configurado =>
        !string.IsNullOrWhiteSpace(Database) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
