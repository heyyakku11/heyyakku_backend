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

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.AnonymousName)
                .IsRequired()
                .HasMaxLength(50);

            builder.HasIndex(x => x.AnonymousName)
                .IsUnique()
                .HasDatabaseName("IX_UserProfiles_AnonymousName");

            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_UserProfiles_UserId");
        }
    }
}
