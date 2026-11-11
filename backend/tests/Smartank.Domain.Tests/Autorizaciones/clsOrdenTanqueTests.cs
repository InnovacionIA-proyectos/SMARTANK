using Smartank.Domain.Autorizaciones.Reglas;

namespace Smartank.Domain.Tests.Autorizaciones;

public class clsOrdenTanqueTests
{
    private static clsDatosOrdenTanqueo Completa() => new(
        Estacion: "EDS Norte",
        GalonesAutorizados: 41m,
        Placa: "ABC123",
        Manifiesto: "301457403",
        Ruta: "Bogota - Medellin",
        MapaUrl: "https://www.google.com/maps?q=4.5,-74.1",
        BitacoraUrl: "https://app.test/bitacora/tok");

    [Fact]
    public void Texto_completo_en_el_orden_del_origen()
    {
        var lineas = clsOrdenTanque.ArmarTexto(Completa()).Split('\n');

        Assert.Equal("\U0001F69A *Orden de tanqueo*", lineas[0]);
        Assert.Equal("Placa: ABC123", lineas[1]);
        Assert.Equal("Manifiesto: 301457403", lineas[2]);
        Assert.Equal("Ruta: Bogota - Medellin", lineas[3]);
        Assert.Equal("Estación: EDS Norte", lineas[4]);
        Assert.Equal("Galones autorizados: 41", lineas[5]);
        Assert.Equal("Ubicación: https://www.google.com/maps?q=4.5,-74.1", lineas[6]);
        Assert.Equal("Reporta tu tanqueo (foto del surtidor + odómetro): https://app.test/bitacora/tok", lineas[7]);
        Assert.Equal(8, lineas.Length);
    }

    [Fact]
    public void Las_lineas_sin_dato_se_omiten()
    {
        var texto = clsOrdenTanque.ArmarTexto(new clsDatosOrdenTanqueo("EDS Sur", 30m, null, null, "  ", null, null));

        Assert.Equal("\U0001F69A *Orden de tanqueo*\nEstación: EDS Sur\nGalones autorizados: 30", texto);
    }

    [Fact]
    public void Los_galones_decimales_se_muestran_sin_ceros_sobrantes_y_con_punto()
    {
        var texto = clsOrdenTanque.ArmarTexto(new clsDatosOrdenTanqueo("E", 12.500m, null, null, null, null, null));

        Assert.Contains("Galones autorizados: 12.5", texto);
    }

    [Fact]
    public void ArmarMapaUrl_usa_punto_decimal_sin_importar_la_cultura()
    {
        Assert.Equal("https://www.google.com/maps?q=4.5,-74.25", clsOrdenTanque.ArmarMapaUrl(4.5, -74.25));
    }

    [Fact]
    public void ArmarMapaUrl_sin_coordenadas_es_null()
    {
        Assert.Null(clsOrdenTanque.ArmarMapaUrl(null, -74.25));
        Assert.Null(clsOrdenTanque.ArmarMapaUrl(4.5, null));
    }
}
