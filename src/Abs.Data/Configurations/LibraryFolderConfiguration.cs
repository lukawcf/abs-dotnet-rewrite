using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class LibraryFolderConfiguration : IEntityTypeConfiguration<LibraryFolder>
{
    public void Configure(EntityTypeBuilder<LibraryFolder> builder)
    {
        builder.ToTable("libraryFolders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Path).HasColumnName("path");
        builder.Property(x => x.LibraryId).HasColumnName("libraryId");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasConversion<AbsDateConverter>();
    }
}
