using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class BookmarkPodcastLookupConfiguration : IEntityTypeConfiguration<BookmarkPodcastLookup>
{
    public void Configure(EntityTypeBuilder<BookmarkPodcastLookup> b)
    {
        b.HasNoKey().ToView("podcasts");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Explicit).HasColumnName("explicit");
        b.Property(x => x.Tags).HasColumnName("tags");
    }
}
