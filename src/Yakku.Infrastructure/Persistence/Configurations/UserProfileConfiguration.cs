using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.ToTable("UserProfiles");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.DisplayName)
                .IsRequired()
                .HasMaxLength(64);

            builder.HasIndex(x => x.DisplayName)
                .IsUnique()
                .HasDatabaseName("IX_UserProfiles_DisplayName");

            builder.Property(x => x.AvatarUrl)
                .HasMaxLength(2048);
        }
    }
}
