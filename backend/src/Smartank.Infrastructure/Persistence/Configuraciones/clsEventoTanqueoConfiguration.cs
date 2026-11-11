using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Autorizaciones.Entidades;
using Smartank.Domain.Estaciones.Entidades;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsEventoTanqueo a dbo.tblEventosTanqueo.</summary>
internal sealed class clsEventoTanqueoConfiguration : IEntityTypeConfiguration<clsEventoTanqueo>
{
    public void Configure(EntityTypeBuilder<clsEventoTanqueo> b)
    {
        b.ConfigurarTabla("tblEventosTanqueo");

        b.Property(x => x.GalonesCargados).HasPrecision(10, 3);
        b.Property(x => x.PrecioPagado).HasPrecision(12, 2);
        b.Property(x => x.FotoSurtidorUrl).HasMaxLength(500);
        b.Property(x => x.OdometroEvento).ComoDecimal(12, 3);
        b.Property(x => x.NivelAntes).HasPrecision(10, 3);
        b.Property(x => x.NivelDespues).HasPrecision(10, 3);
        b.Property(x => x.Programa).HasMaxLength(60);
        b.Property(x => x.Producto).HasMaxLength(120);
        // Si la aplicacion no asigna OcurridoAt, la base usa sysdatetimeoffset().
        b.Property(x => x.OcurridoAt).HasDefaultValueSql("sysdatetimeoffset()").ValueGeneratedOnAdd();

        b.HasOne<clsAutorizacionTanqueo>().WithMany().HasForeignKey(x => x.AutorizacionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblEventosTanqueo_tblAutorizacionesTanqueo_AutorizacionId");
        b.HasOne<clsVehiculo>().WithMany().HasForeignKey(x => x.VehiculoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblEventosTanqueo_tblVehiculos_VehiculoId");
        b.HasOne<clsEstacion>().WithMany().HasForeignKey(x => x.EstacionId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblEventosTanqueo_tblEstaciones_EstacionId");
        b.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.RegistradoPor)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_tblEventosTanqueo_tblUsuarios_RegistradoPor");

        b.HasIndex(x => x.AutorizacionId).IsUnique().HasDatabaseName("UX_tblEventosTanqueo_AutorizacionId");
        b.HasIndex(x => new { x.VehiculoId, x.OcurridoAt }).HasDatabaseName("IX_tblEventosTanqueo_VehiculoId_OcurridoAt");
        b.HasIndex(x => x.EstacionId).HasDatabaseName("IX_tblEventosTanqueo_EstacionId");
        b.HasIndex(x => x.Programa).HasDatabaseName("IX_tblEventosTanqueo_Programa");
        b.HasIndex(x => x.Producto).HasDatabaseName("IX_tblEventosTanqueo_Producto");
    }
}
