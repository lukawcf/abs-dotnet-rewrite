using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthor>
{
    public void Configure(EntityTypeBuilder<BookAuthor> builder)
    {
        builder.ToTable("bookAuthors");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.BookId).HasColumnName("bookId");
        builder.Property(x => x.AuthorId).HasColumnName("authorId");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();

        builder.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId);
        builder.HasOne<Book>().WithMany(x => x.BookAuthors).HasForeignKey(x => x.BookId);
    }
}
