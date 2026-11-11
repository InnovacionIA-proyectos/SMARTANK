using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsHoraColombiaTests
{
    [Fact]
    public void Un_datetime_sin_zona_es_hora_de_Colombia_y_se_pasa_a_UTC_sumando_5_horas()
    {
        var local = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Unspecified);

        var utc = clsHoraColombia.AUtc(local);

        Assert.Equal(new DateTimeOffset(2026, 3, 10, 13, 0, 0, TimeSpan.Zero), utc);
        Assert.Equal(TimeSpan.Zero, utc.Offset);
    }

    [Fact]
    public void Una_hora_tarde_en_Colombia_cruza_al_dia_siguiente_en_UTC()
    {
        var utc = clsHoraColombia.AUtc(new DateTime(2026, 3, 10, 22, 30, 0));

        Assert.Equal(new DateTimeOffset(2026, 3, 11, 3, 30, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Un_datetime_Utc_se_respeta_sin_desplazarlo()
    {
        var utc = clsHoraColombia.AUtc(new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateTimeOffset(2026, 3, 10, 8, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Un_datetime_Local_usa_la_zona_de_la_maquina()
    {
        var local = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Local);

        Assert.Equal(local.ToUniversalTime(), clsHoraColombia.AUtc(local).UtcDateTime);
    }

    [Fact]
    public void El_centinela_9999_12_31_no_desborda_y_queda_en_el_maximo()
    {
        var utc = clsHoraColombia.AUtc(new DateTime(9999, 12, 31, 23, 59, 59));

        Assert.Equal(DateTimeOffset.MaxValue, utc);
    }

    [Fact]
    public void La_variante_nullable_conserva_el_null()
    {
        Assert.Null(clsHoraColombia.AUtc((DateTime?)null));
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 5, 0, 0, TimeSpan.Zero), clsHoraColombia.AUtc((DateTime?)new DateTime(2026, 1, 1)));
    }

    [Fact]
    public void AHoraColombia_es_la_inversa_de_AUtc()
    {
        var original = new DateTime(2026, 7, 4, 15, 45, 10);

        var vuelta = clsHoraColombia.AHoraColombia(clsHoraColombia.AUtc(original));

        Assert.Equal(original, vuelta);
        Assert.Equal(DateTimeKind.Unspecified, vuelta.Kind);
    }

    [Theory]
    [InlineData(2026, 3, 11, 3, 0, 2026, 3, 10)]   // 22:00 del 10 en Colombia
    [InlineData(2026, 3, 11, 4, 59, 2026, 3, 10)]  // 23:59 del 10
    [InlineData(2026, 3, 11, 5, 0, 2026, 3, 11)]   // 00:00 del 11
    [InlineData(2026, 12, 31, 12, 0, 2026, 12, 31)]
    public void FechaColombia_es_el_dia_calendario_de_Colombia_del_instante(
        int a, int m, int d, int h, int min, int ea, int em, int ed)
    {
        var instante = new DateTimeOffset(a, m, d, h, min, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(ea, em, ed), clsHoraColombia.FechaColombia(instante));
    }
}
