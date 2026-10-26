using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
    {
        public void Configure(EntityTypeBuilder<UserSession> builder)
        {
            builder.ToTable("user_sessions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.RefreshTokenHash)
                .IsRequired()
                .HasMaxLength(255);

            // Target of devices' (CurrentSessionId, UserId) foreign key.
            builder.HasAlternateKey(x => new { x.Id, x.UserId })
                .HasName("AK_user_sessions_Id_UserId");

            builder.HasIndex(x => x.DeviceId)
                .HasDatabaseName("IX_UserSessions_DeviceId");

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
