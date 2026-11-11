using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Conductores.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsConductor a dbo.tblConductores.</summary>
internal sealed class clsConductorConfiguration : IEntityTypeConfiguration<clsConductor>
{
    public void Configure(EntityTypeBuilder<clsConductor> b)
    {
        b.ConfigurarTabla("tblConductores");

        b.Property(x => x.Cedula).HasMaxLength(30).IsRequired();
        b.Property(x => x.Nombres).HasMaxLength(200).IsRequired();
        b.Property(x => x.NumeroCelular).HasMaxLength(30);

        b.HasIndex(x => x.Cedula).IsUnique().HasDatabaseName("UX_tblConductores_Cedula");
        b.HasIndex(x => x.FpsapId).IsUnique().HasDatabaseName("UX_tblConductores_FpsapId")
            .HasFilter("[FpsapId] IS NOT NULL");
    }
}
