using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class LibraryConfiguration : IEntityTypeConfiguration<Library>
{
    public void Configure(EntityTypeBuilder<Library> builder)
    {
        builder.ToTable("libraries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Name).HasColumnName("name");
        builder.Property(x => x.DisplayOrder).HasColumnName("displayOrder");
        builder.Property(x => x.Icon).HasColumnName("icon");
        builder.Property(x => x.MediaType).HasColumnName("mediaType");
        builder.Property(x => x.Provider).HasColumnName("provider");
        builder.Property(x => x.LastScan).HasColumnName("lastScan").HasConversion<AbsDateConverter>();
        builder.Property(x => x.LastScanVersion).HasColumnName("lastScanVersion");
        builder.Property(x => x.Settings).HasColumnName("settings");
        builder.Property(x => x.ExtraData).HasColumnName("extraData");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasConversion<AbsDateConverter>();

        builder.HasMany(x => x.Folders)
            .WithOne(x => x.Library)
            .HasForeignKey(x => x.LibraryId);
    }
}
