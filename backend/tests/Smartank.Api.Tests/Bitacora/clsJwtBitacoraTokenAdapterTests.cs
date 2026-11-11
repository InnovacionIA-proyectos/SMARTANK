using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Smartank.Application.Common.Exceptions;
using Smartank.Infrastructure.Bitacora.Adaptadores;

namespace Smartank.Api.Tests.Bitacora;

/// <summary>Adaptador JWT HS256 de la bitacora: emision, validacion, expiracion, proposito y manipulacion.</summary>
public class clsJwtBitacoraTokenAdapterTests
{
    private const string SECRETO = "secreto-de-pruebas-de-la-bitacora-0123456789";
    private static readonly DateTimeOffset AHORA = new(2026, 6, 17, 12, 0, 0, TimeSpan.Zero);

    private sealed class clsReloj(DateTimeOffset ahora) : TimeProvider
    {
        public DateTimeOffset Actual { get; set; } = ahora;

        public override DateTimeOffset GetUtcNow() => Actual;
    }

    private readonly clsReloj _reloj = new(AHORA);
    private readonly clsBitacoraOptions _opciones = new() { Secret = SECRETO, ExpiraHoras = 24 };

    private clsJwtBitacoraTokenAdapter Adaptador() => new(Options.Create(_opciones), _reloj);

    private static string Firmar(
        string secreto, IEnumerable<Claim> claims, DateTime? expira, string algoritmo = SecurityAlgorithms.HmacSha256)
    {
        var token = new JwtSecurityToken(
            claims: claims, notBefore: AHORA.UtcDateTime.AddMinutes(-1), expires: expira,
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secreto)), algoritmo));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static Claim[] Claims(Guid id, string proposito = "bitacora") =>
        [new Claim("autorizacion_id", id.ToString()), new Claim("proposito", proposito)];

    private static string Base64Url(string texto) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(texto)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string CuerpoJson(Guid id) =>
        $"{{\"autorizacion_id\":\"{id}\",\"proposito\":\"bitacora\",\"exp\":{AHORA.AddHours(1).ToUnixTimeSeconds()}}}";

    [Fact]
    public void Emitir_y_validar_devuelve_el_mismo_id()
    {
        var id = Guid.NewGuid();

        var token = Adaptador().Emitir(id);

        Assert.Equal(id, Adaptador().Validar(token));
    }

    [Fact]
    public void El_token_lleva_los_claims_autorizacion_id_y_proposito_y_algoritmo_HS256()
    {
        var id = Guid.NewGuid();

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Adaptador().Emitir(id));

        Assert.Equal("HS256", jwt.Header.Alg);
        Assert.Equal(id.ToString(), jwt.Claims.First(c => c.Type == "autorizacion_id").Value);
        Assert.Equal("bitacora", jwt.Claims.First(c => c.Type == "proposito").Value);
        Assert.Equal(AHORA.UtcDateTime.AddHours(24), jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void La_vigencia_sale_de_ExpiraHoras()
    {
        _opciones.ExpiraHoras = 2;

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(Adaptador().Emitir(Guid.NewGuid()));

        Assert.Equal(AHORA.UtcDateTime.AddHours(2), jwt.ValidTo, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Un_token_dentro_de_su_vigencia_valida()
    {
        var id = Guid.NewGuid();
        var token = Adaptador().Emitir(id);
        _reloj.Actual = AHORA.AddHours(23);

        Assert.Equal(id, Adaptador().Validar(token));
    }

    [Fact]
    public void Un_token_expirado_lanza_401_BITACORA_TOKEN_EXPIRADO()
    {
        var token = Adaptador().Emitir(Guid.NewGuid());
        _reloj.Actual = AHORA.AddHours(24).AddMinutes(5); // pasado el desfase de reloj de 30 s

        var ex = Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token));

        Assert.Equal("BITACORA_TOKEN_EXPIRADO", ex.Code);
    }

    [Fact]
    public void Un_token_firmado_con_otro_secreto_es_invalido()
    {
        var token = Firmar("otro-secreto-distinto-de-32-caracteres-o-mas!", Claims(Guid.NewGuid()), AHORA.UtcDateTime.AddHours(1));

        var ex = Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token));

        Assert.Equal("BITACORA_TOKEN_INVALIDO", ex.Code);
    }

    [Fact]
    public void Un_token_de_otro_proposito_es_invalido_aunque_comparta_secreto()
    {
        var token = Firmar(SECRETO, Claims(Guid.NewGuid(), proposito: "sesion"), AHORA.UtcDateTime.AddHours(1));

        var ex = Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token));

        Assert.Equal("BITACORA_TOKEN_INVALIDO", ex.Code);
    }

    [Fact]
    public void Un_token_sin_proposito_es_invalido()
    {
        var token = Firmar(SECRETO, [new Claim("autorizacion_id", Guid.NewGuid().ToString())], AHORA.UtcDateTime.AddHours(1));

        Assert.Equal("BITACORA_TOKEN_INVALIDO", Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token)).Code);
    }

    [Theory]
    [InlineData("no-es-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Una_autorizacion_id_mal_formada_o_vacia_es_invalida(string id)
    {
        var token = Firmar(SECRETO, [new Claim("autorizacion_id", id), new Claim("proposito", "bitacora")], AHORA.UtcDateTime.AddHours(1));

        Assert.Equal("BITACORA_TOKEN_INVALIDO", Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token)).Code);
    }

    [Fact]
    public void Un_token_sin_expiracion_es_invalido()
    {
        var token = Firmar(SECRETO, Claims(Guid.NewGuid()), expira: null);

        Assert.Equal("BITACORA_TOKEN_INVALIDO", Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token)).Code);
    }

    [Fact]
    public void Un_token_con_otro_algoritmo_HS512_es_invalido()
    {
        var secreto512 = SECRETO + SECRETO; // HS512 pide llave de al menos 512 bits
        var token = Firmar(secreto512, Claims(Guid.NewGuid()), AHORA.UtcDateTime.AddHours(1), SecurityAlgorithms.HmacSha512);

        Assert.Equal("BITACORA_TOKEN_INVALIDO", Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token)).Code);
    }

    [Fact]
    public void Un_token_con_alg_none_es_invalido()
    {
        var token = $"{Base64Url("{\"alg\":\"none\",\"typ\":\"JWT\"}")}.{Base64Url(CuerpoJson(Guid.NewGuid()))}.";

        Assert.Equal("BITACORA_TOKEN_INVALIDO", Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token)).Code);
    }

    [Fact]
    public void Un_token_alterado_en_el_cuerpo_pierde_la_firma()
    {
        var partes = Adaptador().Emitir(Guid.NewGuid()).Split('.');

        var ex = Assert.Throws<clsUnauthorizedException>(
            () => Adaptador().Validar($"{partes[0]}.{Base64Url(CuerpoJson(Guid.NewGuid()))}.{partes[2]}"));

        Assert.Equal("BITACORA_TOKEN_INVALIDO", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("a.b.c")]
    public void Un_token_vacio_o_basura_es_invalido(string token)
    {
        Assert.Equal("BITACORA_TOKEN_INVALIDO", Assert.Throws<clsUnauthorizedException>(() => Adaptador().Validar(token)).Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corto")]
    public void Sin_un_secreto_valido_no_se_emite_ni_valida_y_responde_503(string? secreto)
    {
        _opciones.Secret = secreto;

        var ex1 = Assert.Throws<clsServiceUnavailableException>(() => Adaptador().Emitir(Guid.NewGuid()));
        var ex2 = Assert.Throws<clsServiceUnavailableException>(() => Adaptador().Validar("a.b.c"));

        Assert.Equal("BITACORA_SECRET_NO_CONFIGURADO", ex1.Code);
        Assert.Equal("BITACORA_SECRET_NO_CONFIGURADO", ex2.Code);
    }

    [Fact]
    public void SecretoValido_exige_32_caracteres_y_la_vigencia_por_defecto_es_24_horas()
    {
        Assert.False(new clsBitacoraOptions { Secret = new string('a', 31) }.SecretoValido);
        Assert.True(new clsBitacoraOptions { Secret = new string('a', 32) }.SecretoValido);
        Assert.Equal(24, new clsBitacoraOptions().ExpiraHoras);
    }
}
