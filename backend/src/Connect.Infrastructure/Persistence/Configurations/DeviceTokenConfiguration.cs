using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class DeviceTokenConfiguration : IEntityTypeConfiguration<DeviceToken>
{
    public void Configure(EntityTypeBuilder<DeviceToken> builder)
    {
        builder.HasKey(dt => dt.Id);

        builder.Property(dt => dt.Token)
            .HasMaxLength(500)
            .IsRequired();

        builder.HasIndex(dt => dt.Token)
            .IsUnique();

        builder.HasIndex(dt => dt.UserId);

        builder.Property(dt => dt.Platform)
            .HasConversion<byte>()
            .IsRequired();

        builder.HasOne(dt => dt.User)
            .WithMany(u => u.DeviceTokens)
            .HasForeignKey(dt => dt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
