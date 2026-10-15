using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class MediaItemShareConfiguration : IEntityTypeConfiguration<MediaItemShare>
{
    public void Configure(EntityTypeBuilder<MediaItemShare> builder)
    {
        builder.ToTable("mediaItemShares");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MediaItemId).HasColumnName("mediaItemId");
        builder.Property(x => x.MediaItemType).HasColumnName("mediaItemType");
    }
}
