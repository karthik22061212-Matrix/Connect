using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.UserId)
            .HasMaxLength(30)
            .IsRequired();

        builder.HasIndex(u => u.UserId)
            .IsUnique();

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PhoneNumber)
            .HasMaxLength(20);

        builder.HasIndex(u => u.PhoneNumber)
            .IsUnique()
            .HasFilter("[PhoneNumber] IS NOT NULL");

        builder.Property(u => u.PasswordHash)
            .IsRequired();

        builder.Property(u => u.PresenceStatus)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(u => u.SubscriptionTier)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(u => u.IsDeleted)
            .IsRequired();
    }
}
