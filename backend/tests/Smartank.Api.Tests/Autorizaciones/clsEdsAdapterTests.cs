using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Autorizaciones.Interfaces;
using Smartank.Infrastructure.Autorizaciones.Adaptadores;

namespace Smartank.Api.Tests.Autorizaciones;

public class clsEdsAdapterTests
{
    private static readonly DateTimeOffset AHORA = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class clsReloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => AHORA;
    }

    private static clsPreautorizacionEdsSolicitud Solicitud() => new("ABC123", 40m, AHORA, AHORA.AddHours(12));

    [Fact]
    public async Task Sin_url_base_confirma_de_forma_simulada_y_marcada()
    {
        var a = new clsEdsAdapter(Options.Create(new clsEdsOptions()), new clsReloj());

        var r = await a.RegistrarPreautorizacionAsync(Solicitud(), default);

        Assert.True(r.Ok);
        Assert.True(r.Simulado);
        Assert.Equal($"SIMULADO-ABC123-{AHORA.ToUnixTimeMilliseconds()}", r.Referencia);
        Assert.Equal(AHORA, r.RecibidoAt);
    }

    [Fact]
    public async Task Con_url_base_y_sin_integracion_real_responde_ok_false_sin_fingir_confirmacion()
    {
        var a = new clsEdsAdapter(Options.Create(new clsEdsOptions { ApiBaseUrl = "https://eds.test", ApiKey = "k" }), new clsReloj());

        var r = await a.RegistrarPreautorizacionAsync(Solicitud(), default);

        Assert.False(r.Ok);
        Assert.False(r.Simulado);
        Assert.Null(r.Referencia);
    }
}
