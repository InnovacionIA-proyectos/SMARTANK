using System.Text.Json;
using Smartank.Domain.Enums;
using Smartank.Infrastructure.Sincronizacion.Adaptadores;

namespace Smartank.Api.Tests.Sincronizacion;

/// <summary>Interpretacion de las respuestas publicas de Terpel y Distracom (sin red).</summary>
public class clsPreciosHttpClientTests
{
    private static JsonElement Json(string texto) => JsonDocument.Parse(texto).RootElement.Clone();

    [Fact]
    public void Terpel_toma_ACPM_o_Diesel_y_corriente_filtra_precios_fuera_de_banda_y_coordenadas_fuera_de_Colombia()
    {
        var lista = clsPreciosHttpClient.InterpretarTerpel(Json("""
            [
              {"nom":"EDS UNO","lat":4.711,"lon":-74.07,"price":[{"productName":"acpm","retailPrice":10500},{"productName":"CORRIENTE","retailPrice":13000}]},
              {"nom":"EDS DIESEL","lat":5.0,"lon":-75.0,"price":[{"productName":"DIESEL","retailPrice":10400},{"productName":"CORRIENTE","retailPrice":1}]},
              {"nom":"EDS LEJOS","lat":40.4,"lon":-3.7,"price":[]},
              {"nom":"SIN COORDENADAS","price":[]}
            ]
            """));

        Assert.Equal(3, lista.Count);                       // la que no trae coordenadas no entra
        Assert.All(lista, e => Assert.Equal(FuenteTanqueo.Terpel, e.Proveedor));
        Assert.Equal((10_500m, 13_000m), (lista[0].Acpm, lista[0].Corriente));
        Assert.Equal((10_400m, (decimal?)null), (lista[1].Acpm, lista[1].Corriente));   // 1 = placeholder
        Assert.Null(lista[2].Lat);                          // coordenada basura: se conserva la EDS sin ubicacion
        Assert.Null(lista[2].Lng);
        Assert.Null(lista[0].CodigoExterno);
    }

    [Fact]
    public void Terpel_que_responde_un_objeto_de_error_es_un_fallo_de_la_fuente_y_no_cero_estaciones()
    {
        var ex = Assert.Throws<InvalidDataException>(() => clsPreciosHttpClient.InterpretarTerpel(
            Json("""{"status":500,"message":"Se produjo un error general al procesar la API de Events EDS"}""")));

        Assert.Contains("Se produjo un error general", ex.Message);
    }

    [Fact]
    public void Distracom_usa_EstacionID_como_codigo_y_DIESEL_como_ACPM()
    {
        var lista = clsPreciosHttpClient.InterpretarDistracom(Json("""
            {"data":[
              {"EstacionID":55,"NombreEstacion":"DISTRACOM ALLEGRO","Latitud":4.7,"Longitud":-74.0,"DIESEL":10499,"CORRIENTE":12000},
              {"EstacionID":56,"NombreEstacion":"DISTRACOM X","Latitud":"5.5","Longitud":"-75.5","DIESEL":0,"CORRIENTE":112450},
              {"EstacionID":57,"NombreEstacion":"SIN COORDENADAS"}
            ]}
            """));

        Assert.Equal(2, lista.Count);
        Assert.Equal("55", lista[0].CodigoExterno);
        Assert.Equal((10_499m, 12_000m), (lista[0].Acpm, lista[0].Corriente));
        Assert.Equal(5.5, lista[1].Lat);                    // numero escrito como texto: el origen usaba Number()
        Assert.Null(lista[1].Acpm);
        Assert.Null(lista[1].Corriente);                    // 112450 fuera de banda
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"data":null}""")]
    [InlineData("[]")]
    public void Distracom_sin_datos_es_una_lista_vacia(string json)
    {
        Assert.Empty(clsPreciosHttpClient.InterpretarDistracom(Json(json)));
    }
}
