using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class LibraryItemConfiguration : IEntityTypeConfiguration<LibraryItem>
{
    public void Configure(EntityTypeBuilder<LibraryItem> builder)
    {
        builder.ToTable("libraryItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Ino).HasColumnName("ino");
        builder.Property(x => x.Path).HasColumnName("path");
        builder.Property(x => x.RelPath).HasColumnName("relPath");
        builder.Property(x => x.MediaId).HasColumnName("mediaId");
        builder.Property(x => x.MediaType).HasColumnName("mediaType");
        builder.Property(x => x.IsFile).HasColumnName("isFile");
        builder.Property(x => x.IsMissing).HasColumnName("isMissing");
        builder.Property(x => x.IsInvalid).HasColumnName("isInvalid");
        builder.Property(x => x.Mtime).HasColumnName("mtime").HasConversion<AbsDateConverter>();
        builder.Property(x => x.Ctime).HasColumnName("ctime").HasConversion<AbsDateConverter>();
        builder.Property(x => x.Birthtime).HasColumnName("birthtime").HasConversion<AbsDateConverter>();
        builder.Property(x => x.Size).HasColumnName("size");
        builder.Property(x => x.LastScan).HasColumnName("lastScan").HasConversion<AbsDateConverter>();
        builder.Property(x => x.LastScanVersion).HasColumnName("lastScanVersion");
        builder.Property(x => x.LibraryFiles).HasColumnName("libraryFiles");
        builder.Property(x => x.ExtraData).HasColumnName("extraData");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.TitleIgnorePrefix).HasColumnName("titleIgnorePrefix");
        builder.Property(x => x.AuthorNamesFirstLast).HasColumnName("authorNamesFirstLast");
        builder.Property(x => x.AuthorNamesLastFirst).HasColumnName("authorNamesLastFirst");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.LibraryId).HasColumnName("libraryId");
        builder.Property(x => x.LibraryFolderId).HasColumnName("libraryFolderId");

        // No database foreign key exists for mediaId; this relationship is a join only.
        builder.HasOne(x => x.Book)
            .WithMany()
            .HasForeignKey(x => x.MediaId)
            .IsRequired(false);
    }
}
