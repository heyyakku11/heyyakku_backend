using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
    {
        public void Configure(EntityTypeBuilder<UserSession> builder)
        {
            builder.ToTable("UserSessions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.RefreshTokenHash)
                .IsRequired()
                .HasMaxLength(255);

            builder.HasIndex(x => x.RefreshTokenHash)
                .IsUnique()
                .HasDatabaseName("IX_UserSessions_RefreshTokenHash");

            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_UserSessions_UserId");

            builder.HasOne(x => x.User)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Device)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.DeviceId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
