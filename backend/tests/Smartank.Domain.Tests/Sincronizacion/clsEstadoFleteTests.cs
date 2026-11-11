using Smartank.Domain.Enums;
using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsEstadoFleteTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 1, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Borrado_es_descartado_aunque_este_cerrado_por_SAP_y_el_despacho_ya_haya_pasado()
    {
        var estado = clsEstadoFlete.Derivar(borrado: true, AHORA.AddDays(-3), cerradoPorSap: true, AHORA);

        Assert.Equal(EstadoViaje.Descartado, estado);
    }

    [Fact]
    public void Cerrado_por_SAP_es_cerrado_aunque_el_despacho_sea_futuro()
    {
        var estado = clsEstadoFlete.Derivar(borrado: false, AHORA.AddDays(2), cerradoPorSap: true, AHORA);

        Assert.Equal(EstadoViaje.Cerrado, estado);
    }

    [Fact]
    public void Despacho_pasado_sin_cierre_es_en_ruta()
    {
        var estado = clsEstadoFlete.Derivar(false, AHORA.AddHours(-1), false, AHORA);

        Assert.Equal(EstadoViaje.EnRuta, estado);
    }

    [Fact]
    public void Despacho_exactamente_ahora_ya_es_en_ruta_porque_la_comparacion_es_menor_o_igual()
    {
        var estado = clsEstadoFlete.Derivar(false, AHORA, false, AHORA);

        Assert.Equal(EstadoViaje.EnRuta, estado);
    }

    [Fact]
    public void Despacho_futuro_sin_cierre_es_programado()
    {
        var estado = clsEstadoFlete.Derivar(false, AHORA.AddMinutes(1), false, AHORA);

        Assert.Equal(EstadoViaje.Programado, estado);
    }

    [Fact]
    public void Sin_fecha_de_despacho_es_programado_porque_no_se_puede_considerar_en_ruta()
    {
        var estado = clsEstadoFlete.Derivar(false, null, false, AHORA);

        Assert.Equal(EstadoViaje.Programado, estado);
    }

    // La hora del legado es de Colombia: 10:00 del 1 de junio local son las 15:00 UTC. Con la lectura ingenua del origen
    // (tratarla como UTC) un flete de las 10:00 locales se vería "en ruta" desde las 10:00Z, cinco horas antes del despacho real.
    [Fact]
    public void Con_la_hora_de_Colombia_convertida_a_UTC_un_despacho_de_las_10_locales_sigue_programado_a_las_14_30_UTC()
    {
        var despacho = clsHoraColombia.AUtc(new DateTime(2026, 6, 1, 10, 0, 0));

        Assert.Equal(EstadoViaje.Programado, clsEstadoFlete.Derivar(false, despacho, false, AHORA));
        Assert.Equal(EstadoViaje.EnRuta, clsEstadoFlete.Derivar(false, despacho, false, AHORA.AddMinutes(30)));   // 15:00Z
    }

    [Fact]
    public void Leida_como_UTC_ingenuamente_la_misma_hora_daria_en_ruta_cinco_horas_antes()
    {
        var ingenua = new DateTimeOffset(new DateTime(2026, 6, 1, 10, 0, 0), TimeSpan.Zero);

        Assert.Equal(EstadoViaje.EnRuta, clsEstadoFlete.Derivar(false, ingenua, false, AHORA));
    }
}
