using Microsoft.Extensions.Options;
using Smartank.Application.CasosDeUso.Auth.DTOs;
using Smartank.Domain.Enums;
using Smartank.Infrastructure.Auth;
using Smartank.Infrastructure.Auth.Adaptadores;

namespace Smartank.Api.Tests.Auth;

/// <summary>Adaptadores de seguridad de Infrastructure: bcrypt y JWT. No necesitan base de datos.</summary>
public class clsAutenticacionAdaptadoresTests
{
    private const string SECRETO = "un-secreto-de-pruebas-de-al-menos-32-caracteres!!";

    private static readonly clsUsuarioDto USUARIO = new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "maria@empresa.co", "María", Rol.ControlFlota);

    private static clsJwtSesionTokenAdapter Adaptador(string? secreto = SECRETO, double horas = 12, TimeProvider? reloj = null) =>
        new(Options.Create(new clsJwtOptions { Secret = secreto, ExpiraHoras = horas }), reloj ?? TimeProvider.System);

    private static clsBcryptPasswordHasher Hasher(int costo = 4) => new(Options.Create(new clsSeguridadOptions { BcryptRounds = costo }));

    // --- bcrypt ---

    [Fact]
    public void Hash_y_verificar_dan_ida_y_vuelta()
    {
        var hasher = Hasher();
        var hash = hasher.Hash("una-clave-larga-123");

        Assert.StartsWith("$2", hash);
        Assert.NotEqual("una-clave-larga-123", hash);
        Assert.True(hasher.Verificar("una-clave-larga-123", hash));
        Assert.False(hasher.Verificar("otra-clave", hash));
    }

    [Fact]
    public void El_mismo_texto_produce_hashes_distintos_por_la_sal()
    {
        var hasher = Hasher();

        Assert.NotEqual(hasher.Hash("clave"), hasher.Hash("clave"));
    }

    [Fact]
    public void Verifica_un_hash_2a_del_sistema_anterior_vector_de_prueba_de_crypt_blowfish()
    {
        // Vector publico de crypt_blowfish ($2a$, costo 5, "U*U"): garantiza compatibilidad con los hashes bcryptjs del origen.
        Assert.True(Hasher().Verificar("U*U", "$2a$05$CCCCCCCCCCCCCCCCCCCCC.E5YPO9kmyuRGyh0XouQYb4YMJKvyOeW"));
        Assert.False(Hasher().Verificar("U*V", "$2a$05$CCCCCCCCCCCCCCCCCCCCC.E5YPO9kmyuRGyh0XouQYb4YMJKvyOeW"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-es-un-hash")]
    [InlineData("$2a$12$corto")]
    public void Un_hash_corrupto_devuelve_false_en_vez_de_lanzar(string hash)
    {
        Assert.False(Hasher().Verificar("clave", hash));
    }

    [Fact]
    public void El_costo_configurado_queda_en_el_hash()
    {
        Assert.StartsWith("$2a$06$", Hasher(6).Hash("clave"));
    }

    [Fact]
    public void El_hash_ficticio_es_valido_estable_y_no_corresponde_a_ninguna_clave_conocida()
    {
        var hasher = Hasher();

        Assert.Equal(hasher.HashFicticio, hasher.HashFicticio);
        Assert.StartsWith("$2a$04$", hasher.HashFicticio);
        // Verificar contra el no lanza (consume el mismo trabajo que un hash real) y rechaza claves comunes.
        Assert.False(hasher.Verificar("password", hasher.HashFicticio));
    }

    // --- JWT ---

    [Fact]
    public void Un_token_emitido_se_valida_y_devuelve_id_correo_y_rol()
    {
        var adaptador = Adaptador();
        var sesion = adaptador.Emitir(USUARIO);

        var claims = adaptador.Validar(sesion.Token);

        Assert.NotNull(claims);
        Assert.Equal(USUARIO.Id, claims!.UsuarioId);
        Assert.Equal("maria@empresa.co", claims.Email);
        Assert.Equal(Rol.ControlFlota, claims.Rol);
    }

    [Fact]
    public void El_vencimiento_de_la_sesion_respeta_las_horas_configuradas()
    {
        var ahora = new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        var sesion = Adaptador(horas: 3, reloj: new RelojFijo(ahora)).Emitir(USUARIO);

        Assert.Equal(ahora.AddHours(3), sesion.ExpiraAt);
    }

    [Fact]
    public void El_token_lleva_el_rol_como_nombre_del_enum_para_Authorize_Roles()
    {
        var token = Adaptador().Emitir(USUARIO).Token;
        var payload = System.Text.Encoding.UTF8.GetString(Base64UrlDecodificar(token.Split('.')[1]));

        Assert.Contains("\"http://schemas.microsoft.com/ws/2008/06/identity/claims/role\":\"ControlFlota\"", payload);
        Assert.Contains("\"sub\":\"aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa\"", payload);
        Assert.DoesNotContain("password", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_token_vencido_no_valida()
    {
        // Emitido hace 2 dias con vigencia de 1 hora: vencio hace mucho mas que la holgura de reloj.
        var viejo = Adaptador(horas: 1, reloj: new RelojFijo(DateTimeOffset.UtcNow.AddDays(-2))).Emitir(USUARIO);

        Assert.Null(Adaptador().Validar(viejo.Token));
    }

    [Fact]
    public void Un_token_firmado_con_otra_clave_no_valida()
    {
        var ajeno = Adaptador("otro-secreto-completamente-distinto-de-32-chars!!").Emitir(USUARIO);

        Assert.Null(Adaptador().Validar(ajeno.Token));
    }

    [Fact]
    public void Un_token_manipulado_no_valida()
    {
        var partes = Adaptador().Emitir(USUARIO).Token.Split('.');
        var adulterado = $"{partes[0]}.{partes[1]}x.{partes[2]}";

        Assert.Null(Adaptador().Validar(adulterado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("basura")]
    [InlineData("a.b.c")]
    public void Texto_que_no_es_un_jwt_no_valida(string token)
    {
        Assert.Null(Adaptador().Validar(token));
    }

    [Fact]
    public void Un_token_sin_firma_alg_none_no_valida()
    {
        // {"alg":"none","typ":"JWT"}.{"sub":"...","exp":9999999999}. (sin firma)
        const string SIN_FIRMA = "eyJhbGciOiJub25lIiwidHlwIjoiSldUIn0.eyJzdWIiOiJhYWFhYWFhYS1hYWFhLWFhYWEtYWFhYS1hYWFhYWFhYWFhYWEiLCJleHAiOjk5OTk5OTk5OTl9.";

        Assert.Null(Adaptador().Validar(SIN_FIRMA));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("corto")]
    public void Sin_secreto_valido_emitir_falla_con_un_mensaje_claro(string? secreto)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Adaptador(secreto).Emitir(USUARIO));

        Assert.Contains("Jwt__Secret", ex.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Vigencia_no_positiva_es_un_error_de_configuracion(double horas)
    {
        Assert.NotNull(new clsJwtOptions { Secret = SECRETO, ExpiraHoras = horas }.ErrorDeConfiguracion());
    }

    private static byte[] Base64UrlDecodificar(string s)
    {
        var b64 = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '='));
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
