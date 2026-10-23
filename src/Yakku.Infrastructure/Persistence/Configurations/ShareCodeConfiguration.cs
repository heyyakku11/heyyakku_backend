using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class ShareCodeConfiguration : IEntityTypeConfiguration<ShareCode>
    {
        public void Configure(EntityTypeBuilder<ShareCode> builder)
        {
            builder.ToTable("share_codes", table =>
            {
                table.HasCheckConstraint(
                    "CK_share_codes_content_type",
                    "\"content_type\" IN ('POLL', 'CHECK')");
                table.HasCheckConstraint(
                    "CK_share_codes_code_not_blank",
                    "char_length(btrim(\"code\")) > 0");
                // Exactly one target, and it matches the declared type.
                table.HasCheckConstraint(
                    "CK_share_codes_one_target",
                    """
                    ("content_type" = 'POLL' AND "poll_id" IS NOT NULL AND "user_check_id" IS NULL)
                    OR
                    ("content_type" = 'CHECK' AND "poll_id" IS NULL AND "user_check_id" IS NOT NULL)
                    """);
            });

            builder.HasKey(x => x.Code);

            builder.Property(x => x.Code)
                .HasColumnName("code")
                .HasMaxLength(ShareCodeGenerator.MaxLength)
                .IsRequired();

            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.PollId).HasColumnName("poll_id");
            builder.Property(x => x.UserCheckId).HasColumnName("user_check_id");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            // One code per piece of content.
            builder.HasIndex(x => x.PollId)
                .IsUnique()
                .HasFilter("\"poll_id\" IS NOT NULL")
                .HasDatabaseName("IX_share_codes_poll_id");

            builder.HasIndex(x => x.UserCheckId)
                .IsUnique()
                .HasFilter("\"user_check_id\" IS NOT NULL")
                .HasDatabaseName("IX_share_codes_user_check_id");

            builder.HasOne<Poll>()
                .WithMany()
                .HasForeignKey(x => x.PollId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<UserCheck>()
                .WithMany()
                .HasForeignKey(x => x.UserCheckId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
