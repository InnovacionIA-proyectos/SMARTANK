using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Precios.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsViajePrecioRef a dbo.tblViajePreciosRef (la llave es ViajeId, sin columna Id).</summary>
internal sealed class clsViajePrecioRefConfiguration : IEntityTypeConfiguration<clsViajePrecioRef>
{
    public void Configure(EntityTypeBuilder<clsViajePrecioRef> b)
    {
        b.ToTable("tblViajePreciosRef", t => t.UseSqlOutputClause(false));
        b.HasKey(x => x.ViajeId);
        b.Property(x => x.ViajeId).ValueGeneratedNever();
        b.ConfigurarFechasAuditoria();

        b.Property(x => x.AcpmMin).HasPrecision(12, 2);
        b.Property(x => x.AcpmMax).HasPrecision(12, 2);
        b.Property(x => x.CalculadoAt).HasDefaultValueSql("sysdatetimeoffset()").ValueGeneratedOnAdd();

        b.HasOne<clsViaje>().WithOne().HasForeignKey<clsViajePrecioRef>(x => x.ViajeId)
            .OnDelete(DeleteBehavior.Cascade).HasConstraintName("FK_tblViajePreciosRef_tblViajes_ViajeId");
    }
}
