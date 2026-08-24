using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class ConnectionConfiguration : IEntityTypeConfiguration<Connection>
{
    public void Configure(EntityTypeBuilder<Connection> builder)
    {
        builder.HasKey(c => c.Id);

        builder.HasIndex(c => new { c.UserAId, c.UserBId })
            .IsUnique();

        builder.HasOne(c => c.UserA)
            .WithMany(u => u.ConnectionsA)
            .HasForeignKey(c => c.UserAId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.UserB)
            .WithMany(u => u.ConnectionsB)
            .HasForeignKey(c => c.UserBId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
