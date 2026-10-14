using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class BookConfiguration : IEntityTypeConfiguration<Book>
{
    public void Configure(EntityTypeBuilder<Book> builder)
    {
        builder.ToTable("books");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.TitleIgnorePrefix).HasColumnName("titleIgnorePrefix");
        builder.Property(x => x.Subtitle).HasColumnName("subtitle");
        builder.Property(x => x.PublishedYear).HasColumnName("publishedYear");
        builder.Property(x => x.PublishedDate).HasColumnName("publishedDate");
        builder.Property(x => x.Publisher).HasColumnName("publisher");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.Isbn).HasColumnName("isbn");
        builder.Property(x => x.Asin).HasColumnName("asin");
        builder.Property(x => x.Language).HasColumnName("language");
        builder.Property(x => x.Explicit).HasColumnName("explicit");
        builder.Property(x => x.Abridged).HasColumnName("abridged");
        builder.Property(x => x.CoverPath).HasColumnName("coverPath");
        builder.Property(x => x.Duration).HasColumnName("duration");
        builder.Property(x => x.Narrators).HasColumnName("narrators");
        builder.Property(x => x.AudioFiles).HasColumnName("audioFiles");
        builder.Property(x => x.EbookFile).HasColumnName("ebookFile");
        builder.Property(x => x.Chapters).HasColumnName("chapters");
        builder.Property(x => x.Tags).HasColumnName("tags");
        builder.Property(x => x.Genres).HasColumnName("genres");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasConversion<AbsDateConverter>();
    }
}
