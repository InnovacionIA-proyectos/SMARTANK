using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Enums;
using Smartank.Domain.Rutas.Entidades;
using Smartank.Domain.Viajes.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsRuta a dbo.tblRutas.</summary>
internal sealed class clsRutaConfiguration : IEntityTypeConfiguration<clsRuta>
{
    public void Configure(EntityTypeBuilder<clsRuta> b)
    {
        b.ConfigurarTabla("tblRutas");

        b.Property(x => x.Fo).HasMaxLength(30).IsRequired();
        b.Property(x => x.Fuente).HasMaxLength(20).HasConversion<clsEnumSnakeCaseConverter<FuenteRuta>>();
        b.Property(x => x.Kilometros).ComoDecimal(12, 3);

        // JSON: la polilinea viaja como double[][] y se guarda como texto JSON [[lat,lng],...].
        b.Property(x => x.Polilinea)
            .HasColumnType("nvarchar(max)")
            .HasConversion(clsConfiguracionComun.ConversorPolilinea, clsConfiguracionComun.ComparadorPolilinea);
        b.Property(x => x.DetalleKm).ComoJson();

        // Extremos derivados por el trigger TRG_tblRutas_Derivados: EF los lee y nunca los escribe.
        b.Property(x => x.OrigenLat).ComoDecimal(10, 7).SoloLecturaPorBd();
        b.Property(x => x.OrigenLng).ComoDecimal(10, 7).SoloLecturaPorBd();
        b.Property(x => x.DestinoLat).ComoDecimal(10, 7).SoloLecturaPorBd();
        b.Property(x => x.DestinoLng).ComoDecimal(10, 7).SoloLecturaPorBd();

        b.HasOne<clsViaje>().WithMany().HasForeignKey(x => x.ViajeId)
            .OnDelete(DeleteBehavior.SetNull).HasConstraintName("FK_tblRutas_tblViajes_ViajeId");

        b.HasIndex(x => x.Fo).IsUnique().HasDatabaseName("UX_tblRutas_Fo");
        b.HasIndex(x => x.ViajeId).HasDatabaseName("IX_tblRutas_ViajeId");
    }
}
