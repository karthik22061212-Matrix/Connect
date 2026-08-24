using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class CallConfiguration : IEntityTypeConfiguration<Call>
{
    public void Configure(EntityTypeBuilder<Call> builder)
    {
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Status)
            .HasConversion<byte>()
            .IsRequired();

        builder.Property(c => c.MissedReason)
            .HasConversion<byte>();

        builder.HasIndex(c => new { c.CallerId, c.StartedAt });
        builder.HasIndex(c => new { c.CalleeId, c.StartedAt });

        builder.HasOne(c => c.Connection)
            .WithMany(conn => conn.Calls)
            .HasForeignKey(c => c.ConnectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.Caller)
            .WithMany(u => u.CallsMade)
            .HasForeignKey(c => c.CallerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Callee)
            .WithMany(u => u.CallsReceived)
            .HasForeignKey(c => c.CalleeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
