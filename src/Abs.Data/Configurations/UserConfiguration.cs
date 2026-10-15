using Abs.Data.Conversions;
using Abs.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Abs.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.Username).HasColumnName("username");
        builder.Property(x => x.Email).HasColumnName("email");
        builder.Property(x => x.Type).HasColumnName("type");
        builder.Property(x => x.Token).HasColumnName("token");
        builder.Property(x => x.IsActive).HasColumnName("isActive");
        builder.Property(x => x.IsLocked).HasColumnName("isLocked");
        builder.Property(x => x.LastSeen).HasColumnName("lastSeen").HasConversion<AbsDateConverter>();
        builder.Property(x => x.Permissions).HasColumnName("permissions");
        builder.Property(x => x.ExtraData).HasColumnName("extraData");
    }
}
