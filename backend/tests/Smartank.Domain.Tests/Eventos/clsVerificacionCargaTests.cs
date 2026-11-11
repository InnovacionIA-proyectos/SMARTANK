using Smartank.Domain.Enums;
using Smartank.Domain.Eventos.Reglas;

namespace Smartank.Domain.Tests.Eventos;

/// <summary>
/// Verificacion de la carga con los umbrales por defecto del origen: tolerancia 0.02 y umbral de galon fantasma 0.05.
/// Cada alerta se prueba justo en su umbral exacto (no alerta, la comparacion es estricta) y apenas pasado.
/// </summary>
public class clsVerificacionCargaTests
{
    private const double TOLERANCIA = 0.02;
    private const double UMBRAL_FANTASMA = 0.05;

    private static clsResultadoVerificacion Evaluar(
        decimal cargados, decimal? antes = null, decimal? despues = null, decimal autorizados = 40m,
        decimal? precioEstacion = null, decimal? precioReferencia = null) =>
        clsVerificacionCarga.Evaluar(new clsDatosVerificacion(
            cargados, antes, despues, autorizados, precioEstacion, precioReferencia, TOLERANCIA, UMBRAL_FANTASMA));

    // ---- Estado final -------------------------------------------------------------------------------------------------

    [Fact]
    public void Una_carga_completa_sin_novedades_queda_ejecutada_sin_alertas()
    {
        var r = Evaluar(40m, antes: 10m, despues: 50m);

        Assert.Equal(EstadoAutorizacion.Ejecutada, r.EstadoFinal);
        Assert.Empty(r.Alertas);
    }

    // ---- Ejecucion parcial --------------------------------------------------------------------------------------------

    [Fact]
    public void Ejecucion_parcial_en_el_umbral_exacto_no_alerta()
    {
        // 40 x (1 - 0.02) = 39.2: cargar exactamente 39.2 NO es parcial (la regla es cargado < minimo).
        var r = Evaluar(39.2m);

        Assert.Equal(EstadoAutorizacion.Ejecutada, r.EstadoFinal);
        Assert.Empty(r.Alertas);
    }

    [Fact]
    public void Ejecucion_parcial_justo_debajo_del_umbral_alerta_con_la_diferencia()
    {
        var r = Evaluar(39.19m);

        Assert.Equal(EstadoAutorizacion.EjecutadaParcial, r.EstadoFinal);
        var alerta = Assert.Single(r.Alertas);
        Assert.Equal(TipoAlerta.EjecucionParcial, alerta.Tipo);
        Assert.Equal(0.81, alerta.Magnitud, 9); // autorizado - cargado = 40 - 39.19
    }

    [Fact]
    public void Cargar_30_de_40_es_parcial_con_magnitud_10()
    {
        var r = Evaluar(30m);

        Assert.Equal(EstadoAutorizacion.EjecutadaParcial, r.EstadoFinal);
        Assert.Equal(10, Assert.Single(r.Alertas).Magnitud, 9);
    }

    [Fact]
    public void Cargar_mas_de_lo_autorizado_no_es_parcial()
    {
        var r = Evaluar(45m);

        Assert.Equal(EstadoAutorizacion.Ejecutada, r.EstadoFinal);
        Assert.Empty(r.Alertas);
    }

    // ---- Desviacion de consumo por carga ("galon fantasma") ----------------------------------------------------------

    [Fact]
    public void Desviacion_de_carga_cuando_lo_cargado_supera_el_aumento_de_nivel()
    {
        // Cargo 40, el nivel subio 15 (10 -> 25): faltante 25 > 0.05 x 40 = 2.
        var r = Evaluar(40m, antes: 10m, despues: 25m);

        var alerta = Assert.Single(r.Alertas);
        Assert.Equal(TipoAlerta.DesviacionConsumoCarga, alerta.Tipo);
        Assert.Equal(25, alerta.Magnitud, 9);
        Assert.Equal(EstadoAutorizacion.Ejecutada, r.EstadoFinal); // 40 de 40 no es parcial
    }

    [Fact]
    public void Desviacion_de_carga_en_el_umbral_exacto_no_alerta()
    {
        // Umbral = 0.05 x 40 = 2 galones. Nivel sube 38 -> faltante exactamente 2: no es mayor que el umbral.
        var r = Evaluar(40m, antes: 10m, despues: 48m);

        Assert.Empty(r.Alertas);
    }

    [Fact]
    public void Desviacion_de_carga_apenas_sobre_el_umbral_alerta()
    {
        // Nivel sube 37.99 -> faltante 2.01 > 2.
        var r = Evaluar(40m, antes: 10m, despues: 47.99m);

        var alerta = Assert.Single(r.Alertas);
        Assert.Equal(TipoAlerta.DesviacionConsumoCarga, alerta.Tipo);
        Assert.Equal(2.01, alerta.Magnitud, 9);
    }

    [Fact]
    public void Sin_nivel_antes_o_despues_no_hay_desviacion_de_carga()
    {
        Assert.Empty(Evaluar(40m, antes: null, despues: 50m).Alertas);
        Assert.Empty(Evaluar(40m, antes: 10m, despues: null).Alertas);
        Assert.Empty(Evaluar(40m).Alertas);
    }

    [Fact]
    public void Un_aumento_de_nivel_mayor_al_cargado_no_alerta()
    {
        // faltante negativo (el sensor midio de mas): no es galon fantasma.
        Assert.Empty(Evaluar(40m, antes: 10m, despues: 60m).Alertas);
    }

    [Fact]
    public void La_magnitud_de_la_desviacion_se_redondea_a_dos_decimales()
    {
        // faltante = 40 - 36.3333 = 3.6667 -> 3.67
        var r = Evaluar(40m, antes: 0m, despues: 36.3333m);

        Assert.Equal(3.67, Assert.Single(r.Alertas).Magnitud, 9);
    }

    // ---- Estacion cara -----------------------------------------------------------------------------------------------

    [Fact]
    public void Estacion_cara_cuando_el_precio_supera_la_referencia_de_la_ruta()
    {
        var r = Evaluar(40m, precioEstacion: 9800m, precioReferencia: 9500m);

        var alerta = Assert.Single(r.Alertas);
        Assert.Equal(TipoAlerta.EstacionCara, alerta.Tipo);
        Assert.Equal(300, alerta.Magnitud, 9);
    }

    [Fact]
    public void Estacion_cara_en_el_precio_exacto_de_la_referencia_no_alerta()
    {
        Assert.Empty(Evaluar(40m, precioEstacion: 9500m, precioReferencia: 9500m).Alertas);
    }

    [Fact]
    public void Estacion_cara_con_un_peso_de_diferencia_alerta()
    {
        var alerta = Assert.Single(Evaluar(40m, precioEstacion: 9501m, precioReferencia: 9500m).Alertas);

        Assert.Equal(1, alerta.Magnitud, 9);
    }

    [Fact]
    public void Una_estacion_mas_barata_que_la_referencia_no_alerta()
    {
        Assert.Empty(Evaluar(40m, precioEstacion: 9000m, precioReferencia: 9500m).Alertas);
    }

    [Fact]
    public void Sin_referencia_o_sin_precio_no_hay_alerta_de_estacion_cara()
    {
        Assert.Empty(Evaluar(40m, precioEstacion: 9800m, precioReferencia: null).Alertas);
        Assert.Empty(Evaluar(40m, precioEstacion: null, precioReferencia: 9500m).Alertas);
    }

    // ---- Combinaciones -----------------------------------------------------------------------------------------------

    [Fact]
    public void Las_tres_alertas_salen_en_el_orden_del_origen()
    {
        // Carga 30 de 40 (parcial); nivel sube solo 10 (faltante 20); precio sobre la referencia.
        var r = Evaluar(30m, antes: 10m, despues: 20m, precioEstacion: 9800m, precioReferencia: 9500m);

        Assert.Equal(EstadoAutorizacion.EjecutadaParcial, r.EstadoFinal);
        Assert.Equal(
            [TipoAlerta.DesviacionConsumoCarga, TipoAlerta.EjecucionParcial, TipoAlerta.EstacionCara],
            r.Alertas.Select(a => a.Tipo));
    }

    [Fact]
    public void Una_carga_en_cero_galones_con_autorizacion_es_parcial_y_sin_desviacion()
    {
        // Con 0 cargados el umbral de desviacion es 0 y el faltante es 0 - incremento: sin nivel no hay alerta de carga.
        var r = Evaluar(0m);

        Assert.Equal(EstadoAutorizacion.EjecutadaParcial, r.EstadoFinal);
        Assert.Equal(40, Assert.Single(r.Alertas).Magnitud, 9);
    }
}
