using Connect.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Connect.Infrastructure.Persistence.Configurations;

public class UserPresenceSettingConfiguration : IEntityTypeConfiguration<UserPresenceSetting>
{
    public void Configure(EntityTypeBuilder<UserPresenceSetting> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => x.UserId)
            .IsUnique();

        builder.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<UserPresenceSetting>(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
