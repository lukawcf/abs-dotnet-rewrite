using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class PlaybackProgressConfiguration : IEntityTypeConfiguration<PlaybackProgress>
{
    public void Configure(EntityTypeBuilder<PlaybackProgress> builder)
    {
        builder.ToTable("mediaProgresses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.UserId).HasColumnName("userId");
        builder.Property(x => x.MediaItemId).HasColumnName("mediaItemId");
        builder.Property(x => x.MediaItemType).HasColumnName("mediaItemType");
        builder.Property(x => x.PodcastId).HasColumnName("podcastId");
        builder.Property(x => x.Duration).HasColumnName("duration");
        builder.Property(x => x.CurrentTime).HasColumnName("currentTime");
        builder.Property(x => x.IsFinished).HasColumnName("isFinished");
        builder.Property(x => x.HideFromContinueListening).HasColumnName("hideFromContinueListening");
        builder.Property(x => x.EbookLocation).HasColumnName("ebookLocation");
        builder.Property(x => x.EbookProgress).HasColumnName("ebookProgress");
        builder.Property(x => x.ExtraData).HasColumnName("extraData");
        builder.Property(x => x.FinishedAt).HasColumnName("finishedAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasConversion<AbsDateConverter>();
    }
}
