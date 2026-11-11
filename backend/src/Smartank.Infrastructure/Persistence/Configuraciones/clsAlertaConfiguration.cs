using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Alertas.Entidades;
using Smartank.Domain.Enums;
using Smartank.Domain.Eventos.Entidades;
using Smartank.Domain.Usuarios.Entidades;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsAlerta a dbo.tblAlertas.</summary>
internal sealed class clsAlertaConfiguration : IEntityTypeConfiguration<clsAlerta>
{
    public void Configure(EntityTypeBuilder<clsAlerta> b)
    {
        b.ConfigurarTabla("tblAlertas");

        b.Property(x => x.Tipo).HasMaxLength(30).HasConversion<clsEnumSnakeCaseConverter<TipoAlerta>>();
        b.Property(x => x.MagnitudDesviacion).ComoDecimal(14, 4);
        b.Property(x => x.Estado).HasMaxLength(20).HasConversion<clsEnumSnakeCaseConverter<EstadoAlerta>>();
        b.Property(x => x.RowVersion).IsRowVersion();

        b.HasOne<clsVehiculo>().WithMany().HasForeignKey(x => x.VehiculoId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblAlertas_tblVehiculos_VehiculoId");
        b.HasOne<clsEventoTanqueo>().WithMany().HasForeignKey(x => x.EventoId)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_tblAlertas_tblEventosTanqueo_EventoId");
        b.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.GestionadaPor)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_tblAlertas_tblUsuarios_GestionadaPor");

        b.HasIndex(x => new { x.Estado, x.CreatedAt }).HasDatabaseName("IX_tblAlertas_Estado_CreatedAt");
        b.HasIndex(x => x.VehiculoId).HasDatabaseName("IX_tblAlertas_VehiculoId");
        b.HasIndex(x => x.EventoId).HasDatabaseName("IX_tblAlertas_EventoId");
    }
}
