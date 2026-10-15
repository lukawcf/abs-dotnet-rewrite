using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class FeedConfiguration : IEntityTypeConfiguration<Feed>
{
    public void Configure(EntityTypeBuilder<Feed> builder)
    {
        builder.ToTable("feeds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.EntityType).HasColumnName("entityType");
        builder.Property(x => x.EntityId).HasColumnName("entityId");
        builder.Property(x => x.FeedUrl).HasColumnName("feedURL");
        builder.Property(x => x.Title).HasColumnName("title");
        builder.Property(x => x.Description).HasColumnName("description");
        builder.Property(x => x.OwnerName).HasColumnName("ownerName");
        builder.Property(x => x.OwnerEmail).HasColumnName("ownerEmail");
        builder.Property(x => x.PreventIndexing).HasColumnName("preventIndexing");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();
    }
}
