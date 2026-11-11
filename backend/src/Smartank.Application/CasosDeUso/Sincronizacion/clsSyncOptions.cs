namespace Smartank.Application.CasosDeUso.Sincronizacion;

/// <summary>
/// Parametros de las sincronizaciones y del Worker (seccion de configuracion <c>Sync</c>; variables <c>Sync__*</c>).
/// Las fechas "desde" se interpretan como hora de Colombia (igual que las columnas del legado contra las que se comparan).
/// </summary>
public class clsSyncOptions
{
    public const string SECCION = "Sync";

    private const double TOLERANCIA_POR_DEFECTO_METROS = 30;
    private const double UMBRAL_EDS_POR_DEFECTO_KM = 5;

    /// <summary>Cada cuantos segundos corre el grupo frecuente (telemetria, tanqueos, viajes y rutas incremental).</summary>
    public int IntervaloSegundos { get; set; } = 300;

    /// <summary>Si el grupo diario corre al arrancar el Worker (true) o espera al siguiente dia calendario de Colombia (false).</summary>
    public bool DiarioAlIniciar { get; set; } = true;

    /// <summary>Despacho minimo de los fletes (FOs) a sincronizar.</summary>
    public DateTime FosDesde { get; set; } = new(2026, 1, 1);

    /// <summary>Fecha minima de los tanqueos del historial a sincronizar.</summary>
    public DateTime TanqueosDesde { get; set; } = new(2026, 1, 1);

    /// <summary>Fecha minima de creacion de las rutas Valhalla a considerar.</summary>
    public DateTime RutasDesde { get; set; } = new(2026, 1, 1);

    /// <summary>Tolerancia (metros) de la simplificacion de la polilinea. Un valor invalido (&lt;= 0) usa 30.</summary>
    public double RutaReduceTolMetros { get; set; } = TOLERANCIA_POR_DEFECTO_METROS;

    /// <summary>Horas hacia atras en que se busca la ultima lectura de nivel/odometro (amplia a proposito: un vehiculo parqueado dias no reporta).</summary>
    public int VentanaNivelHoras { get; set; } = 168;

    /// <summary>Dias de la ventana con que se deriva el rendimiento (km/galon).</summary>
    public int RendimientoVentanaDias { get; set; } = 60;

    /// <summary>Distancia maxima (km) de una EDS a la ruta para entrar en el precio de referencia del viaje. Un valor invalido usa 5.</summary>
    public double RutaEdsUmbralKm { get; set; } = UMBRAL_EDS_POR_DEFECTO_KM;

    /// <summary>
    /// Si es true, los viajes sin ruta en Valhalla reciben una linea recta entre los centros poblados DANE de origen y destino
    /// (fuente "dane"). Por defecto false: el origen dejo de generar esas rutas aproximadas (los FO sin Valhalla quedan sin ruta).
    /// </summary>
    public bool RutaDaneFallback { get; set; }

    public double ToleranciaEfectivaMetros =>
        double.IsFinite(RutaReduceTolMetros) && RutaReduceTolMetros > 0 ? RutaReduceTolMetros : TOLERANCIA_POR_DEFECTO_METROS;

    public double UmbralEdsEfectivoKm =>
        double.IsFinite(RutaEdsUmbralKm) && RutaEdsUmbralKm > 0 ? RutaEdsUmbralKm : UMBRAL_EDS_POR_DEFECTO_KM;

    public int VentanaNivelHorasEfectiva => VentanaNivelHoras > 0 ? VentanaNivelHoras : 168;

    public int RendimientoVentanaDiasEfectiva => RendimientoVentanaDias > 0 ? RendimientoVentanaDias : 60;
}
