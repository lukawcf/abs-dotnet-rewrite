using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

// Additive D-owned shadow mapping; the Auth module's User class stays unchanged.
public class BookmarkUserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property<string?>("DBookmarks").HasColumnName("bookmarks");
        builder.Property<DateTimeOffset>("DBookmarkUpdatedAt").HasColumnName("updatedAt").HasConversion<AbsDateConverter>();
    }
}
