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

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.DisplayName)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(x => x.DisplayName)
                .IsUnique()
                .HasDatabaseName("IX_UserProfiles_DisplayName");

            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_UserProfiles_UserId");

            builder.HasOne(x => x.AvatarImage)
                .WithMany(x => x.UserProfiles)
                .HasForeignKey(x => x.AvatarImageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
