using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsViaje a dbo.tblViajes.</summary>
internal sealed class clsViajeConfiguration : IEntityTypeConfiguration<clsViaje>
{
    public void Configure(EntityTypeBuilder<clsViaje> b)
    {
        b.ConfigurarTabla("tblViajes");

        b.Property(x => x.Ruta).HasMaxLength(200).IsRequired();
        b.Property(x => x.Estado).HasMaxLength(20).HasConversion<clsEnumSnakeCaseConverter<EstadoViaje>>();
        b.Property(x => x.Manifiesto).HasMaxLength(60);
        b.Property(x => x.AutonomiaEstimadaKm).ComoDecimal(10, 2);
        // Columna calculada y persistida: inicio real si existe, si no fecha de despacho.
        b.Property(x => x.FechaEfectiva).HasComputedColumnSql("isnull([InicioAt],[FechaDespacho])", stored: true);
        b.Property(x => x.NumeroFo).HasMaxLength(30);
        b.Property(x => x.PlacaCabezote).HasMaxLength(20);
        b.Property(x => x.PlacaTrailer).HasMaxLength(20);
        b.Property(x => x.ProgramaCabezote).HasMaxLength(60);
        b.Property(x => x.CedulaConductor).HasMaxLength(30);
        b.Property(x => x.Transportista).HasMaxLength(200);
        b.Property(x => x.CodigoRuta).HasMaxLength(50);
        b.Property(x => x.NombreRuta).HasMaxLength(250);
        b.Property(x => x.CiudadOrigen).HasMaxLength(100);
        b.Property(x => x.CiudadDestino).HasMaxLength(100);
        b.Property(x => x.PesoFo).HasPrecision(14, 3);
        b.Property(x => x.GalonesEds).HasPrecision(10, 3);
        b.Property(x => x.GalonesCredito).HasPrecision(10, 3);
        b.Property(x => x.GalonesContado).HasPrecision(10, 3);
        b.Property(x => x.GalonesRuta).HasPrecision(10, 3);
        b.Property(x => x.KmtsRuta).ComoDecimal(12, 3);

        b.HasOne<clsVehiculo>().WithMany().HasForeignKey(x => x.VehiculoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblViajes_tblVehiculos_VehiculoId");
        b.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.DespachadoPor)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_tblViajes_tblUsuarios_DespachadoPor");

        b.HasIndex(x => x.VehiculoId).HasDatabaseName("IX_tblViajes_VehiculoId");
        b.HasIndex(x => x.Estado).HasDatabaseName("IX_tblViajes_Estado");
        b.HasIndex(x => x.Ruta).HasDatabaseName("IX_tblViajes_Ruta");
        b.HasIndex(x => x.Manifiesto).HasDatabaseName("IX_tblViajes_Manifiesto");
        b.HasIndex(x => x.FechaDespacho).HasDatabaseName("IX_tblViajes_FechaDespacho");
        b.HasIndex(x => x.PlacaCabezote).HasDatabaseName("IX_tblViajes_PlacaCabezote");
        b.HasIndex(x => x.NumeroFo).HasDatabaseName("IX_tblViajes_NumeroFo");
        b.HasIndex(x => new { x.VehiculoId, x.FechaEfectiva }).IsDescending(false, true)
            .HasDatabaseName("IX_tblViajes_VehiculoId_FechaEfectiva");
        b.HasIndex(x => x.FoId).IsUnique().HasDatabaseName("UX_tblViajes_FoId").HasFilter("[FoId] IS NOT NULL");
    }
}
