using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
    {
        public void Configure(EntityTypeBuilder<UserPermission> builder)
        {
            builder.ToTable("user_permissions");

            // One row per user: the user id is both the key and the foreign key, which makes the 1:1 a hard rule.
            builder.HasKey(x => x.UserId);
            builder.Property(x => x.UserId).HasColumnName("user_id").ValueGeneratedNever();

            builder.Property(x => x.AllowAskBack)
                .HasColumnName("allow_ask_back")
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.PushNotification)
                .HasColumnName("push_notification")
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(x => x.Sms)
                .HasColumnName("sms")
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.WhatsApp)
                .HasColumnName("whatsapp")
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        }
    }
}
