using Connect.Domain.Entities;
using Connect.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class ConnectRequestConfiguration : IEntityTypeConfiguration<ConnectRequest>
{
    public void Configure(EntityTypeBuilder<ConnectRequest> builder)
    {
        builder.HasKey(cr => cr.Id);

        builder.Property(cr => cr.Status)
            .HasConversion<byte>()
            .IsRequired();

        builder.HasIndex(cr => new { cr.FromUserId, cr.ToUserId })
            .IsUnique()
            .HasFilter("[Status] = 0");

        builder.HasOne(cr => cr.FromUser)
            .WithMany(u => u.ConnectRequestsSent)
            .HasForeignKey(cr => cr.FromUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cr => cr.ToUser)
            .WithMany(u => u.ConnectRequestsReceived)
            .HasForeignKey(cr => cr.ToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
