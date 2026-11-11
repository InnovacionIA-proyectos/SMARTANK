using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Auditoria.Entidades;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>
/// Mapeo de clsAuditLog a dbo.tblAuditLog. Tabla append-only: un trigger INSTEAD OF bloquea UPDATE y DELETE, asi que
/// la aplicacion solo inserta. No hay trigger de UpdatedAt.
/// </summary>
internal sealed class clsAuditLogConfiguration : IEntityTypeConfiguration<clsAuditLog>
{
    public void Configure(EntityTypeBuilder<clsAuditLog> b)
    {
        b.ConfigurarTabla("tblAuditLog", updatedAtPorTrigger: false);

        b.Property(x => x.Accion).HasMaxLength(80).IsRequired();
        b.Property(x => x.Entidad).HasMaxLength(80).IsRequired();
        b.Property(x => x.Diff).ComoJson();

        // Sin ON DELETE SET NULL a proposito (ver ddl.sql): un usuario con auditoria no se puede borrar.
        b.HasOne<clsUsuario>().WithMany().HasForeignKey(x => x.ActorId)
            .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_tblAuditLog_tblUsuarios_ActorId");

        b.HasIndex(x => new { x.Entidad, x.EntidadId }).HasDatabaseName("IX_tblAuditLog_Entidad_EntidadId");
        b.HasIndex(x => x.ActorId).HasDatabaseName("IX_tblAuditLog_ActorId");
        b.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_tblAuditLog_CreatedAt");
    }
}
