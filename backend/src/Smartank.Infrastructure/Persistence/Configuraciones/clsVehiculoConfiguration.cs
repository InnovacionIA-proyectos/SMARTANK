using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Vehiculos.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsVehiculo a dbo.tblVehiculos.</summary>
internal sealed class clsVehiculoConfiguration : IEntityTypeConfiguration<clsVehiculo>
{
    public void Configure(EntityTypeBuilder<clsVehiculo> b)
    {
        b.ConfigurarTabla("tblVehiculos");

        b.Property(x => x.Placa).HasMaxLength(20).IsRequired();
        b.Property(x => x.AliasPlaca).HasMaxLength(60);
        b.Property(x => x.TipoMotor).HasMaxLength(60).IsRequired();
        b.Property(x => x.Programa).HasMaxLength(60);
        b.Property(x => x.ChipTanqueo).HasMaxLength(60);
        b.Property(x => x.NitPropietario).HasMaxLength(30);
        b.Property(x => x.CapacidadTanqueGal).HasPrecision(10, 3);
        b.Property(x => x.GalonesReserva).HasPrecision(10, 3);
        b.Property(x => x.GalonesUtilizables).HasPrecision(10, 3);
        b.Property(x => x.GalonesTolerancia).HasPrecision(10, 3);
        b.Property(x => x.RendimientoHistorico).ComoDecimal(8, 4);
        b.Property(x => x.RendimientoEsperado).ComoDecimal(8, 4);
        b.Property(x => x.ToleranciaMotor).ComoDecimal(5, 4);
        b.Property(x => x.Odometro).ComoDecimal(12, 3);
        b.Property(x => x.NivelTanqueActual).HasPrecision(10, 3);
        b.Property(x => x.UltimaLat).ComoDecimal(10, 7);
        b.Property(x => x.UltimaLng).ComoDecimal(10, 7);
        b.Property(x => x.GeotabDeviceId).HasMaxLength(100);

        // Los defaults de la base (Activo = 1, ToleranciaMotor = 0.02) los replica el inicializador de la entidad:
        // declararlos aqui haria que EF omitiera Activo = false (valor CLR por defecto) y la base lo pusiera en true.

        b.HasIndex(x => x.Placa).IsUnique().HasDatabaseName("UX_tblVehiculos_Placa");
        b.HasIndex(x => x.FpsapId).IsUnique().HasDatabaseName("UX_tblVehiculos_FpsapId")
            .HasFilter("[FpsapId] IS NOT NULL");
        b.HasIndex(x => x.GeotabDeviceId).IsUnique().HasDatabaseName("UX_tblVehiculos_GeotabDeviceId")
            .HasFilter("[GeotabDeviceId] IS NOT NULL");
    }
}
