using Smartank.Domain.Sincronizacion.Reglas;

namespace Smartank.Domain.Tests.Sincronizacion;

public class clsCruceEstacionesTests
{
    private static readonly clsEstacionReferencia CON_CODIGO = new(Guid.NewGuid(), "Distracom Allegro", 4.7110, -74.0721, "123");
    private static readonly clsEstacionReferencia CERCA_A = new(Guid.NewGuid(), "Terpel Norte", 4.7110, -74.0721, null);
    private static readonly clsEstacionReferencia CERCA_B = new(Guid.NewGuid(), "Terpel Sur", 4.7120, -74.0721, null);   // 111 m al norte de A
    private static readonly clsEstacionReferencia SIN_COORDENADAS = new(Guid.NewGuid(), "Terpel  La Pista", null, null, null);

    private static clsCruceEstaciones Cruce() => new([CON_CODIGO, CERCA_A, CERCA_B, SIN_COORDENADAS]);

    [Fact]
    public void Distracom_cruza_por_codigo_externo_aunque_el_nombre_y_las_coordenadas_no_coincidan()
    {
        var encontrada = Cruce().Encontrar("123", "Nombre distinto", 10, 10);

        Assert.Same(CON_CODIGO, encontrada);
    }

    [Fact]
    public void Terpel_cruza_por_la_estacion_mas_cercana_dentro_de_200_metros()
    {
        // 33 m de A y 78 m de B: gana A.
        var encontrada = Cruce().Encontrar(null, "Otro nombre", 4.7113, -74.0721);

        Assert.Same(CERCA_A, encontrada);
    }

    [Fact]
    public void Una_estacion_a_mas_de_200_metros_no_cruza_por_coordenadas_y_cae_al_nombre_normalizado()
    {
        // ~330 m de B: fuera del radio; el nombre ("TERPEL LA PISTA") si coincide aunque cambien espacios y mayusculas.
        var porNombre = Cruce().Encontrar(null, "terpel la pista", 4.7150, -74.0721);

        Assert.Same(SIN_COORDENADAS, porNombre);
    }

    [Fact]
    public void Sin_coincidencia_alguna_la_estacion_es_nueva()
    {
        Assert.Null(Cruce().Encontrar(null, "Terpel Desconocida", 4.9, -74.5));
        Assert.Null(Cruce().Encontrar("999", "Distracom Inexistente", null, null));
    }

    [Fact]
    public void El_nombre_se_compara_sin_tildes()
    {
        var c = new clsCruceEstaciones([new clsEstacionReferencia(Guid.NewGuid(), "Distracom Doña Juana", null, null, null)]);

        Assert.NotNull(c.Encontrar(null, "DISTRACOM DONA JUANA", null, null));
    }

    [Fact]
    public void Un_catalogo_vacio_nunca_encuentra()
    {
        Assert.Null(new clsCruceEstaciones([]).Encontrar("1", "X", 4, -74));
    }
}
