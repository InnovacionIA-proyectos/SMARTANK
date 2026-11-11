namespace Smartank.Infrastructure.Bitacora.Adaptadores;

/// <summary>Configuracion del token firmado de la bitacora del conductor (seccion "Bitacora": Bitacora__Secret, Bitacora__ExpiraHoras).</summary>
public class clsBitacoraOptions
{
    public const string SECCION = "Bitacora";

    /// <summary>Largo minimo del secreto (HS256 pide una llave de al menos 256 bits).</summary>
    public const int LONGITUD_MINIMA_SECRETO = 32;

    /// <summary>Secreto de firma HS256 (minimo 32 caracteres; secreto, distinto del de la sesion). Sin el, la bitacora no funciona.</summary>
    public string? Secret { get; set; }

    /// <summary>Vigencia del token en horas desde que se notifica al conductor.</summary>
    public double ExpiraHoras { get; set; } = 24;

    /// <summary>True si el secreto existe y tiene el largo minimo; el host puede exigirlo al arrancar.</summary>
    public bool SecretoValido => Secret is { } s && s.Length >= LONGITUD_MINIMA_SECRETO;
}
