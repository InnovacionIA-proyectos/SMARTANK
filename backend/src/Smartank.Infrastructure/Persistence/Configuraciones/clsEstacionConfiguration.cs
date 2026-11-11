using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Estaciones.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsEstacion a dbo.tblEstaciones.</summary>
internal sealed class clsEstacionConfiguration : IEntityTypeConfiguration<clsEstacion>
{
    public void Configure(EntityTypeBuilder<clsEstacion> b)
    {
        b.ConfigurarTabla("tblEstaciones");

        b.Property(x => x.CodigoExterno).HasMaxLength(50);
        b.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
        // Columna calculada y persistida: la base la mantiene, EF solo la lee.
        b.Property(x => x.NombreNormalizado).HasMaxLength(200)
            .HasComputedColumnSql("lower(ltrim(rtrim([Nombre])))", stored: true);
        b.Property(x => x.Ruta).HasMaxLength(200);
        b.Property(x => x.Lat).ComoDecimal(10, 7);
        b.Property(x => x.Lng).ComoDecimal(10, 7);
        b.Property(x => x.Proveedor).HasMaxLength(60);
        b.Property(x => x.Producto).HasMaxLength(120);
        b.Property(x => x.PrecioGalonActual).HasPrecision(12, 2);
        b.Property(x => x.PrecioAcpm).HasPrecision(12, 2);
        b.Property(x => x.PrecioCorriente).HasPrecision(12, 2);

        b.HasIndex(x => x.Ruta).HasDatabaseName("IX_tblEstaciones_Ruta");
        b.HasIndex(x => new { x.Ruta, x.PrecioGalonActual }).HasDatabaseName("IX_tblEstaciones_Ruta_PrecioGalonActual");
        b.HasIndex(x => new { x.Lat, x.Lng }).HasDatabaseName("IX_tblEstaciones_Lat_Lng");
        b.HasIndex(x => x.Proveedor).HasDatabaseName("IX_tblEstaciones_Proveedor");
        b.HasIndex(x => x.Producto).HasDatabaseName("IX_tblEstaciones_Producto");
        b.HasIndex(x => x.CodigoExterno).HasDatabaseName("IX_tblEstaciones_CodigoExterno");
        b.HasIndex(x => x.NombreNormalizado).HasDatabaseName("IX_tblEstaciones_NombreNormalizado");
    }
}
