using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class BookSeriesConfiguration : IEntityTypeConfiguration<BookSeries>
{
    public void Configure(EntityTypeBuilder<BookSeries> builder)
    {
        builder.ToTable("bookSeries");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BookId).HasColumnName("bookId");
        builder.Property(x => x.SeriesId).HasColumnName("seriesId");
        builder.Property(x => x.Sequence).HasColumnName("sequence");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();

        builder.HasOne(x => x.Series).WithMany().HasForeignKey(x => x.SeriesId);
        builder.HasOne<Book>().WithMany(x => x.BookSeries).HasForeignKey(x => x.BookId);
    }
}
