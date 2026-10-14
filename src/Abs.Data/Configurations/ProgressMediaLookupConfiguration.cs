using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class ProgressItemLookupConfiguration : IEntityTypeConfiguration<ProgressItemLookup>
{
    public void Configure(EntityTypeBuilder<ProgressItemLookup> b)
    {
        b.HasNoKey().ToView("libraryItems");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.MediaId).HasColumnName("mediaId");
        b.Property(x => x.MediaType).HasColumnName("mediaType");
        b.Property(x => x.LibraryId).HasColumnName("libraryId");
    }
}
public class ProgressBookLookupConfiguration : IEntityTypeConfiguration<ProgressBookLookup>
{
    public void Configure(EntityTypeBuilder<ProgressBookLookup> b)
    {
        b.HasNoKey().ToView("books");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Explicit).HasColumnName("explicit");
        b.Property(x => x.Tags).HasColumnName("tags");
    }
}
public class ProgressEpisodeLookupConfiguration : IEntityTypeConfiguration<ProgressEpisodeLookup>
{
    public void Configure(EntityTypeBuilder<ProgressEpisodeLookup> b)
    {
        b.HasNoKey().ToView("podcastEpisodes");
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.PodcastId).HasColumnName("podcastId");
    }
}
