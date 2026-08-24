using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.HasKey(b => b.Id);

        builder.HasIndex(b => new { b.BlockerUserId, b.BlockedUserId })
            .IsUnique();

        builder.HasOne(b => b.BlockerUser)
            .WithMany(u => u.BlocksInitiated)
            .HasForeignKey(b => b.BlockerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.BlockedUser)
            .WithMany(u => u.BlocksReceived)
            .HasForeignKey(b => b.BlockedUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
