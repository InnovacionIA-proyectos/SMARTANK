using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Enums;
using Smartank.Domain.Tanqueos.Entidades;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsTanqueo a dbo.tblTanqueos.</summary>
internal sealed class clsTanqueoConfiguration : IEntityTypeConfiguration<clsTanqueo>
{
    public void Configure(EntityTypeBuilder<clsTanqueo> b)
    {
        b.ConfigurarTabla("tblTanqueos");

        b.Property(x => x.Fuente).HasMaxLength(20).HasConversion<clsEnumSnakeCaseConverter<FuenteTanqueo>>();
        b.Property(x => x.Placa).HasMaxLength(20).IsRequired();
        b.Property(x => x.Galones).HasPrecision(10, 3);
        b.Property(x => x.ValorGalon).HasPrecision(12, 2);
        b.Property(x => x.ValorTotal).HasPrecision(16, 2);
        b.Property(x => x.Producto).HasMaxLength(120);
        b.Property(x => x.FamiliaProducto).HasMaxLength(120);
        b.Property(x => x.Ciudad).HasMaxLength(120);
        b.Property(x => x.Estacion).HasMaxLength(200);
        b.Property(x => x.Kilometraje).ComoDecimal(12, 3);
        b.Property(x => x.CedulaConductor).HasMaxLength(30);
        b.Property(x => x.Factura).HasMaxLength(60);
        b.Property(x => x.Referencia).HasMaxLength(100);
        b.Property(x => x.CentroCosto).HasMaxLength(100);

        b.HasOne<clsVehiculo>().WithMany().HasForeignKey(x => x.VehiculoId)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_tblTanqueos_tblVehiculos_VehiculoId");

        b.HasIndex(x => new { x.Fuente, x.FuenteId }).IsUnique().HasDatabaseName("UX_tblTanqueos_Fuente_FuenteId");
        b.HasIndex(x => x.VehiculoId).HasDatabaseName("IX_tblTanqueos_VehiculoId");
        b.HasIndex(x => x.Placa).HasDatabaseName("IX_tblTanqueos_Placa");
        b.HasIndex(x => x.Fecha).HasDatabaseName("IX_tblTanqueos_Fecha");
    }
}
