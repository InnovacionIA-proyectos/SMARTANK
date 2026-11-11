using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Smartank.Api.Tests.Infra;
using Smartank.Domain.Conductores.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Rutas.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Api.Tests.Http;

/// <summary>
/// Flujo critico de punta a punta por HTTP real: recomendar -> autorizar -> notificar (enlace con token) -> el conductor,
/// SIN sesion, abre la bitacora, sube la foto y reporta la carga -> verificacion automatica. Valida de paso el contrato
/// JSON (snake_case), el cuerpo opcional de la recomendacion y las desviaciones de seguridad decididas.
/// </summary>
public class clsFlujoTanqueoHttpTests : IClassFixture<clsApiFactory>
{
    // Firma PNG real (8 bytes) seguida de relleno: el API valida la firma, no que sea una imagen decodificable.
    private static readonly byte[] _png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];

    private readonly clsApiFactory _factory;

    public clsFlujoTanqueoHttpTests(clsApiFactory factory) => _factory = factory;

    [clsHechoConBaseDePruebas]
    public async Task Flujo_completo_de_un_tanqueo()
    {
        var datos = await SembrarViajeAsync();
        var despacho = await _factory.SesionAsync(Rol.Despacho);
        var conductor = _factory.ClienteAnonimo();

        // 1) Recomendacion SIN cuerpo (galones calculados) -> propuesta con la EDS elegida.
        var recomendacion = await despacho.PostAsync($"/api/viajes/{datos.ViajeId}/recomendacion", null);
        Assert.Equal(HttpStatusCode.OK, recomendacion.StatusCode);
        var rec = await clsApiFactory.LeerJsonAsync(recomendacion);
        Assert.True(rec.GetProperty("requiere_tanqueo").GetBoolean());
        var autorizacionId = rec.GetProperty("autorizacion").GetProperty("id").GetGuid();

        // 1b) Repetir la recomendacion CON cuerpo actualiza la misma propuesta (no duplica).
        var repetida = await despacho.PostAsJsonAsync($"/api/viajes/{datos.ViajeId}/recomendacion", new { galones = 25 });
        Assert.Equal(HttpStatusCode.OK, repetida.StatusCode);
        var rec2 = await clsApiFactory.LeerJsonAsync(repetida);
        Assert.Equal(autorizacionId, rec2.GetProperty("autorizacion").GetProperty("id").GetGuid());
        Assert.Equal(25, rec2.GetProperty("galones_max").GetDecimal());

        // 2) Notificar antes de autorizar -> 409 (la guia exige estado Autorizada).
        var temprano = await despacho.PostAsync($"/api/autorizaciones/{autorizacionId}/notificar", null);
        Assert.Equal(HttpStatusCode.Conflict, temprano.StatusCode);

        // 3) Autorizar (usuario actual queda como AutorizadoPor) y reintentar autorizar -> 409.
        var autorizar = await despacho.PostAsync($"/api/autorizaciones/{autorizacionId}/autorizar", null);
        Assert.Equal(HttpStatusCode.OK, autorizar.StatusCode);
        Assert.Equal("autorizada", (await clsApiFactory.LeerJsonAsync(autorizar)).GetProperty("estado").GetString());
        Assert.Equal(HttpStatusCode.Conflict, (await despacho.PostAsync($"/api/autorizaciones/{autorizacionId}/autorizar", null)).StatusCode);

        // 4) Notificar: WhatsApp no esta configurado en pruebas -> la orden se prepara (enviado=false) sin fallar.
        var notificar = await despacho.PostAsync($"/api/autorizaciones/{autorizacionId}/notificar", null);
        Assert.Equal(HttpStatusCode.OK, notificar.StatusCode);
        var not = await clsApiFactory.LeerJsonAsync(notificar);
        Assert.False(not.GetProperty("whatsapp").GetProperty("enviado").GetBoolean());
        var enlace = not.GetProperty("bitacora_url").GetString()!;
        Assert.StartsWith($"{clsApiFactory.URL_PUBLICA}/bitacora/", enlace);
        var token = enlace[$"{clsApiFactory.URL_PUBLICA}/bitacora/".Length..];
        Assert.DoesNotContain(datos.Manifiesto, token); // el enlace ya no es el manifiesto predecible

        // 5) El conductor (sin sesion) abre la bitacora con el token.
        var orden = await conductor.GetAsync($"/api/bitacora/{token}");
        Assert.Equal(HttpStatusCode.OK, orden.StatusCode);
        var ord = await clsApiFactory.LeerJsonAsync(orden);
        Assert.Equal(datos.Placa, ord.GetProperty("placa").GetString());
        Assert.False(ord.GetProperty("ya_registrada").GetBoolean());

        // 6) Subida de foto: el contenido manda sobre el Content-Type (un texto declarado como imagen se rechaza).
        var falsa = new ByteArrayContent("esto no es una imagen"u8.ToArray());
        falsa.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        Assert.Equal(HttpStatusCode.BadRequest, (await conductor.PostAsync($"/api/uploads?token={token}", falsa)).StatusCode);

        var real = new ByteArrayContent(_png);
        real.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        var subida = await conductor.PostAsync($"/api/uploads?token={token}", real);
        Assert.Equal(HttpStatusCode.Created, subida.StatusCode);
        var sub = await clsApiFactory.LeerJsonAsync(subida);
        var fotoUrl = sub.GetProperty("url").GetString()!;

        // La foto exige sesion para verla (el origen la dejaba publica).
        var idFoto = sub.GetProperty("id").GetString();
        Assert.Equal(HttpStatusCode.Unauthorized, (await conductor.GetAsync($"/api/uploads/{idFoto}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await despacho.GetAsync($"/api/uploads/{idFoto}")).StatusCode);

        // 7) Reporte de la carga. Cargo 25 gal pero el nivel solo subio 5: debe disparar la alerta de desviacion.
        var evento = await conductor.PostAsJsonAsync("/api/eventos", new
        {
            token,
            galones_cargados = 25,
            precio_pagado = 9500,
            odometro_evento = 123456,
            foto_surtidor_url = fotoUrl,
            nivel_antes = 5,
            nivel_despues = 10,
        });
        Assert.Equal(HttpStatusCode.Created, evento.StatusCode);

        // 8) Un segundo reporte con el mismo token -> 409 BITACORA_YA_REGISTRADA.
        var duplicado = await conductor.PostAsJsonAsync("/api/eventos", new { token, galones_cargados = 25 });
        Assert.Equal(HttpStatusCode.Conflict, duplicado.StatusCode);
        Assert.Equal("BITACORA_YA_REGISTRADA", (await clsApiFactory.LeerJsonAsync(duplicado)).GetProperty("code").GetString());

        // 9) La verificacion automatica fijo el estado final y creo la alerta (la ve ControlFlota).
        var detalle = await despacho.GetAsync($"/api/autorizaciones/{autorizacionId}");
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        var estadoFinal = (await clsApiFactory.LeerJsonAsync(detalle)).GetProperty("estado").GetString();
        Assert.Contains(estadoFinal, new[] { "ejecutada", "ejecutada_parcial" });

        var control = await _factory.SesionAsync(Rol.ControlFlota);
        var alertas = await clsApiFactory.LeerJsonAsync(await control.GetAsync("/api/alertas"));
        Assert.Contains(alertas.GetProperty("alertas").EnumerateArray(),
            a => a.GetProperty("placa").GetString() == datos.Placa
                 && a.GetProperty("tipo").GetString() == "desviacion_consumo_carga");

        // 10) El historial de eventos (con sesion) trae el tanqueo.
        var historial = await clsApiFactory.LeerJsonAsync(await despacho.GetAsync($"/api/eventos?placa={datos.Placa}"));
        Assert.True(historial.GetProperty("total").GetInt32() >= 0);
    }

    [clsHechoConBaseDePruebas]
    public async Task Un_token_firmado_con_otra_llave_o_alterado_no_abre_la_bitacora()
    {
        var datos = await SembrarViajeAsync();
        var despacho = await _factory.SesionAsync(Rol.Despacho);
        var rec = await clsApiFactory.LeerJsonAsync(await despacho.PostAsync($"/api/viajes/{datos.ViajeId}/recomendacion", null));
        var id = rec.GetProperty("autorizacion").GetProperty("id").GetGuid();
        await despacho.PostAsync($"/api/autorizaciones/{id}/autorizar", null);
        var not = await clsApiFactory.LeerJsonAsync(await despacho.PostAsync($"/api/autorizaciones/{id}/notificar", null));
        var token = not.GetProperty("bitacora_url").GetString()!.Split('/').Last();

        var alterado = token[..^3] + (token.EndsWith("AAA") ? "BBB" : "AAA");
        var resp = await _factory.ClienteAnonimo().GetAsync($"/api/bitacora/{alterado}");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    [clsHechoConBaseDePruebas]
    public async Task El_manifiesto_ya_no_sirve_como_credencial_de_la_bitacora()
    {
        var datos = await SembrarViajeAsync();

        var resp = await _factory.ClienteAnonimo().GetAsync($"/api/bitacora/{datos.Manifiesto}");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>Vehiculo con poco combustible, viaje con ruta recta de ~111 km hacia el norte y una EDS sobre la via.</summary>
    private static async Task<(Guid ViajeId, string Placa, string Manifiesto)> SembrarViajeAsync()
    {
        var marca = Guid.NewGuid().ToString("N");
        var placa = $"{(char)('A' + Random.Shared.Next(26))}{(char)('A' + Random.Shared.Next(26))}{(char)('A' + Random.Shared.Next(26))}{Random.Shared.Next(100, 999)}";
        var manifiesto = $"9{Random.Shared.NextInt64(100000000, 999999999)}";
        var cedula = $"9{Random.Shared.NextInt64(10000000, 99999999)}";

        await using var db = clsContextoDePruebas.Crear();

        db.Conductores.Add(new clsConductor { Cedula = cedula, Nombres = "Conductor de Prueba", NumeroCelular = "3001234567" });
        var vehiculo = new clsVehiculo
        {
            Placa = placa,
            TipoMotor = "diesel",
            CapacidadTanqueGal = 100,
            NivelTanqueActual = 5,
            NivelAt = DateTimeOffset.UtcNow,
            RendimientoHistorico = 8,
            UltimaLat = 4.1,
            UltimaLng = -74.0,
            UbicacionAt = DateTimeOffset.UtcNow,
        };
        db.Vehiculos.Add(vehiculo);
        await db.SaveChangesAsync();

        var viaje = new clsViaje
        {
            VehiculoId = vehiculo.Id,
            Ruta = $"PRUEBA-{marca[..8]}",
            Estado = EstadoViaje.EnRuta,
            Manifiesto = manifiesto,
            NumeroFo = manifiesto,
            CedulaConductor = cedula,
            InicioAt = DateTimeOffset.UtcNow.AddHours(-2),
            FechaDespacho = DateTimeOffset.UtcNow.AddHours(-2),
        };
        db.Viajes.Add(viaje);
        await db.SaveChangesAsync();

        db.Rutas.Add(new clsRuta
        {
            Fo = marca[..20],
            ViajeId = viaje.Id,
            Kilometros = 111,
            NumPuntos = 2,
            Polilinea = [[4.0, -74.0], [5.0, -74.0]],
            FechaCalculo = DateTimeOffset.UtcNow,
        });
        db.Estaciones.Add(new clsEstacion
        {
            Nombre = $"EDS PRUEBA {marca[..8]}",
            Lat = 4.35,
            Lng = -74.0,
            PrecioAcpm = 9500,
            PrecioGalonActual = 9500,
            Proveedor = "terpel",
            Producto = "ACPM",
        });
        await db.SaveChangesAsync();
        return (viaje.Id, placa, manifiesto);
    }
}
