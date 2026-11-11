using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Smartank.Domain.Uploads.Entidades;

namespace Smartank.Infrastructure.Persistence.Configuraciones;

/// <summary>Mapeo de clsUpload a dbo.tblUploads (imagenes como varbinary(max); inmutable, sin trigger de UpdatedAt).</summary>
internal sealed class clsUploadConfiguration : IEntityTypeConfiguration<clsUpload>
{
    public void Configure(EntityTypeBuilder<clsUpload> b)
    {
        b.ConfigurarTabla("tblUploads", updatedAtPorTrigger: false);

        b.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        b.Property(x => x.Data).HasColumnType("varbinary(max)").IsRequired();
    }
}
