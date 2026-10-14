using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class SettingConfiguration : IEntityTypeConfiguration<Setting>
{
    public void Configure(EntityTypeBuilder<Setting> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasColumnName("key");
        builder.Property(x => x.Value).HasColumnName("value");
        builder.Property(x => x.CreatedAt).HasColumnName("createdAt").HasConversion<AbsDateConverter>();
        builder.Property(x => x.UpdatedAt).HasColumnName("updatedAt").HasConversion<AbsDateConverter>();
    }
}
