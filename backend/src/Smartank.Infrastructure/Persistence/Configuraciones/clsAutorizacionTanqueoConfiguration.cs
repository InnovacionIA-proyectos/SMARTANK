using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Domain.Vehiculos.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsAutorizacionTanqueo a dbo.tblAutorizacionesTanqueo.</summary>
internal sealed class clsAutorizacionTanqueoConfiguration : IEntityTypeConfiguration<clsAutorizacionTanqueo>
{
    public void Configure(EntityTypeBuilder<clsAutorizacionTanqueo> b)
    {
        b.ConfigurarTabla("tblAutorizacionesTanqueo");

        b.Property(x => x.GalonesMax).HasPrecision(10, 3);
        b.Property(x => x.PrecioTecho).HasPrecision(12, 2);
        b.Property(x => x.DeltaAhorroEstimado).HasPrecision(16, 2);
        b.Property(x => x.Estado).HasMaxLength(20).HasConversion<clsEnumSnakeCaseConverter<EstadoAutorizacion>>();
        b.Property(x => x.EdsConfirmacion).ComoJson();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<clsViaje>().WithMany().HasForeignKey(x => x.ViajeId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblAutorizacionesTanqueo_tblViajes_ViajeId");
        b.HasOne<clsVehiculo>().WithMany().HasForeignKey(x => x.VehiculoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblAutorizacionesTanqueo_tblVehiculos_VehiculoId");
        b.HasOne<clsEstacion>().WithMany().HasForeignKey(x => x.EstacionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblAutorizacionesTanqueo_tblEstaciones_EstacionId");
        b.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.AutorizadoPor)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_tblAutorizacionesTanqueo_tblUsuarios_AutorizadoPor");

        b.HasIndex(x => new { x.VehiculoId, x.FechaInicio, x.FechaFin })
            .HasDatabaseName("IX_tblAutorizacionesTanqueo_VehiculoId_Ventana");
        b.HasIndex(x => x.ViajeId).HasDatabaseName("IX_tblAutorizacionesTanqueo_ViajeId");
        b.HasIndex(x => x.EstacionId).HasDatabaseName("IX_tblAutorizacionesTanqueo_EstacionId");
        b.HasIndex(x => x.Estado).HasDatabaseName("IX_tblAutorizacionesTanqueo_Estado");
        // A lo sumo una propuesta por viaje (evita duplicados por doble clic o carrera entre usuarios).
        b.HasIndex(x => x.ViajeId).IsUnique().HasDatabaseName("UX_tblAutorizacionesTanqueo_ViajePropuesta")
            .HasFilter("[Estado] = N'propuesta'");
    }
}
