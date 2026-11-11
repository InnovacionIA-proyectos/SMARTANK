using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>
/// Reglas de mapeo que comparten todas las tablas. El esquema real lo define Database/ddl.sql; aqui solo se
/// declara lo necesario para que EF coincida con el (nombres, longitudes, tipos, valores generados por la base).
/// </summary>
internal static class clsConfiguracionComun
{
    private const string DEFAULT_FECHA = "sysdatetimeoffset()";

    /// <summary>
    /// Tabla con llave Id (Guid) y fechas CreatedAt/UpdatedAt. EF genera el Guid secuencial del lado de la aplicacion
    /// (el DEFAULT NEWSEQUENTIALID() de la base no se puede leer sin OUTPUT). Con <paramref name="updatedAtPorTrigger"/>
    /// la base mantiene UpdatedAt (trigger AFTER UPDATE) y EF nunca lo escribe; sin trigger (tablas inmutables) solo
    /// se omite en el INSERT cuando no se asigna.
    /// </summary>
    public static void ConfigurarTabla<T>(this EntityTypeBuilder<T> b, string tabla, bool updatedAtPorTrigger = true)
        where T : class
    {
        // UseSqlOutputClause(false): las tablas con triggers no admiten la clausula OUTPUT que EF usa por defecto.
        b.ToTable(tabla, t => t.UseSqlOutputClause(false));
        b.HasKey("Id");
        b.Property<Guid>("Id").ValueGeneratedOnAdd();
        ConfigurarFechasAuditoria(b, updatedAtPorTrigger);
    }

    /// <summary>CreatedAt/UpdatedAt con el default de la base (sysdatetimeoffset()).</summary>
    public static void ConfigurarFechasAuditoria<T>(this EntityTypeBuilder<T> b, bool updatedAtPorTrigger = true)
        where T : class
    {
        b.Property<DateTimeOffset>("CreatedAt").HasDefaultValueSql(DEFAULT_FECHA).ValueGeneratedOnAdd();
        var updated = b.Property<DateTimeOffset>("UpdatedAt").HasDefaultValueSql(DEFAULT_FECHA);
        if (updatedAtPorTrigger) updated.ValueGeneratedOnAddOrUpdate();
        else updated.ValueGeneratedOnAdd();
    }

    /// <summary>
    /// Propiedad double de la entidad guardada como decimal(p,s): la base conserva precision exacta y el dominio
    /// trabaja con double (geografia, rendimientos, kilometros).
    /// </summary>
    public static PropertyBuilder<double?> ComoDecimal(this PropertyBuilder<double?> p, int precision, int escala) =>
        p.HasColumnType($"decimal({precision},{escala})").HasConversion(ConversorDoubleDecimal);

    /// <summary>Version para propiedades double no nulas.</summary>
    public static PropertyBuilder<double> ComoDecimal(this PropertyBuilder<double> p, int precision, int escala) =>
        p.HasColumnType($"decimal({precision},{escala})").HasConversion(ConversorDoubleDecimal);

    // Un solo conversor sirve para double y double?: EF nunca convierte los null.
    private static readonly ValueConverter<double, decimal> ConversorDoubleDecimal = new(v => (decimal)v, v => (double)v);

    /// <summary>Columna que mantiene un trigger de la base: EF la lee tras guardar pero nunca la escribe.</summary>
    public static PropertyBuilder<double?> SoloLecturaPorBd(this PropertyBuilder<double?> p)
    {
        p.ValueGeneratedOnAddOrUpdate();
        p.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        p.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
        return p;
    }

    /// <summary>Columna nvarchar(max) con JSON (la base la protege con CHECK ISJSON).</summary>
    public static PropertyBuilder<string?> ComoJson(this PropertyBuilder<string?> p) => p.HasColumnType("nvarchar(max)");

    /// <summary>Conversion double[][] &lt;-&gt; JSON para la polilinea ([[lat,lng],...]).</summary>
    public static ValueConverter<double[][]?, string> ConversorPolilinea { get; } =
        new(v => SerializarPolilinea(v), v => DeserializarPolilinea(v));

    /// <summary>Comparador por valor (el arreglo es mutable, EF necesita saber cuando cambia).</summary>
    public static ValueComparer<double[][]?> ComparadorPolilinea { get; } =
        new(
            (a, b) => SerializarPolilinea(a) == SerializarPolilinea(b),
            v => SerializarPolilinea(v).GetHashCode(),
            v => ClonarPolilinea(v));

    private static string SerializarPolilinea(double[][]? v) => JsonSerializer.Serialize(v);

    private static double[][]? DeserializarPolilinea(string v) => JsonSerializer.Deserialize<double[][]>(v);

    private static double[][]? ClonarPolilinea(double[][]? v) => v?.Select(p => p.ToArray()).ToArray();
}
