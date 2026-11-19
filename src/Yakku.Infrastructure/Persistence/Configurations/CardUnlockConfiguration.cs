using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class CardUnlockConfiguration : IEntityTypeConfiguration<CardUnlock>
    {
        public void Configure(EntityTypeBuilder<CardUnlock> builder)
        {
            builder.ToTable("card_unlocks", table =>
            {
                table.HasCheckConstraint(
                    "CK_card_unlocks_source",
                    "\"source\" IN ('PLAN', 'PURCHASE')");
                table.HasCheckConstraint(
                    "CK_card_unlocks_content_type",
                    "\"content_type\" IN ('POLL', 'CHECK')");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.ContentId).HasColumnName("content_id").IsRequired();
            builder.Property(x => x.Source)
                .HasColumnName("source")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20);
            builder.Property(x => x.UserPlanEntitlementId).HasColumnName("user_plan_entitlement_id");
            builder.Property(x => x.PaymentOrderId).HasColumnName("payment_order_id");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            builder.HasOne<User>().WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<UserPlanEntitlement>()
                .WithMany()
                .HasForeignKey(x => x.UserPlanEntitlementId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne<PaymentOrder>()
                .WithMany()
                .HasForeignKey(x => x.PaymentOrderId)
                .OnDelete(DeleteBehavior.SetNull);

            // One unlock per card: unlocking again finds the first one.
            builder.HasIndex(x => new { x.OwnerUserId, x.ContentType, x.ContentId })
                .IsUnique()
                .HasDatabaseName("IX_card_unlocks_owner_card");

            // Tickets are counted per owner from the activation time (and later by entitlement id).
            builder.HasIndex(x => new { x.OwnerUserId, x.Source, x.CreatedAt })
                .HasDatabaseName("IX_card_unlocks_owner_source_created");
            builder.HasIndex(x => x.UserPlanEntitlementId)
                .HasDatabaseName("IX_card_unlocks_user_plan_entitlement_id")
                .HasFilter("\"user_plan_entitlement_id\" IS NOT NULL");
            builder.HasIndex(x => x.PaymentOrderId)
                .HasDatabaseName("IX_card_unlocks_payment_order_id")
                .HasFilter("\"payment_order_id\" IS NOT NULL");
        }
    }

    internal class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
    {
        public void Configure(EntityTypeBuilder<UserBlock> builder)
        {
            builder.ToTable("user_blocks", table =>
            {
                table.HasCheckConstraint(
                    "CK_user_blocks_distinct_users",
                    "\"blocker_user_id\" <> \"blocked_user_id\"");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.BlockerUserId).HasColumnName("blocker_user_id").IsRequired();
            builder.Property(x => x.BlockedUserId).HasColumnName("blocked_user_id").IsRequired();
            builder.Property(x => x.SourceConversationId).HasColumnName("source_conversation_id");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

            // Both sides go away with their account.
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerUserId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedUserId).OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.BlockerUserId, x.BlockedUserId })
                .IsUnique()
                .HasDatabaseName("IX_user_blocks_pair");
            builder.HasIndex(x => x.BlockedUserId)
                .HasDatabaseName("IX_user_blocks_blocked_user_id");
        }
    }
}
