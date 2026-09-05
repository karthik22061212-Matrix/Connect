using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class PresenceVisibilityExceptionConfiguration : IEntityTypeConfiguration<PresenceVisibilityException>
{
    public void Configure(EntityTypeBuilder<PresenceVisibilityException> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.OwnerUserId, x.TargetUserId })
            .IsUnique();

        builder.HasOne(x => x.OwnerUser)
            .WithMany()
            .HasForeignKey(x => x.OwnerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.TargetUser)
            .WithMany()
            .HasForeignKey(x => x.TargetUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
