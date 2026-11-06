using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public const string AnonymousNameIndex = "IX_UserProfiles_AnonymousName";

        /// <summary>Stored lower-case copy of the name, so the unique index is case-insensitive.</summary>
        public const string AnonymousNameKey = "AnonymousNameKey";

        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.ToTable("user_profiles");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.AnonymousName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property<string>(AnonymousNameKey)
                .HasColumnName(AnonymousNameKey)
                .HasComputedColumnSql("lower(\"AnonymousName\")", stored: true);

            builder.HasIndex(AnonymousNameKey)
                .IsUnique()
                .HasDatabaseName(AnonymousNameIndex);

            builder.HasIndex(x => x.UserId)
                .IsUnique()
                .HasDatabaseName("IX_UserProfiles_UserId");
        }
    }
}
