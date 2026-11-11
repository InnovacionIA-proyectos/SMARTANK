namespace Smartank.Application.CasosDeUso.Sincronizacion.Interfaces;

/// <summary>Manifiestos de viajes ya cerrados en SAP: evento ZEND_TRIP con fecha real en el DW de operaciones (servidor Ebs).</summary>
public interface IManifiestosCerradosPort
{
    /// <summary>Manifiestos (numero de FO sin ceros) con cierre desde la fecha dada (hora de Colombia). Sin cadena Ebs lanza EBS_NO_CONFIGURADO.</summary>
    Task<IReadOnlySet<string>> ListarDesdeAsync(DateTime desdeHoraColombia, CancellationToken ct = default);
}
