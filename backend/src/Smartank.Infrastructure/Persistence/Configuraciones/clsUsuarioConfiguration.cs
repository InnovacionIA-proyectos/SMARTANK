using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Enums;
using Smartank.Domain.Usuarios.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsUsuario a dbo.tblUsuarios.</summary>
internal sealed class clsUsuarioConfiguration : IEntityTypeConfiguration<clsUsuario>
{
    public void Configure(EntityTypeBuilder<clsUsuario> b)
    {
        b.ConfigurarTabla("tblUsuarios");

        b.Property(x => x.Email).HasMaxLength(256).IsRequired();
        b.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();
        b.Property(x => x.Nombre).HasMaxLength(200);
        b.Property(x => x.Rol).HasMaxLength(20).HasConversion<clsEnumSnakeCaseConverter<Rol>>();

        b.HasIndex(x => x.Email).IsUnique().HasDatabaseName("UX_tblUsuarios_Email");
    }
}
