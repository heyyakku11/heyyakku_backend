using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollConfiguration : IEntityTypeConfiguration<Poll>
    {
        public void Configure(EntityTypeBuilder<Poll> builder)
        {
            builder.ToTable("polls", table =>
            {
                table.HasCheckConstraint(
                    "CK_polls_content_type",
                    "\"content_type\" IN ('QUESTION', 'IMAGE', 'VIDEO', 'AUDIO')");
                table.HasCheckConstraint(
                    "CK_polls_status",
                    "\"status\" IN ('DRAFT', 'ACTIVE', 'CLOSED', 'EXPIRED')");
                table.HasCheckConstraint(
                    "CK_polls_visibility",
                    "\"visibility\" IN ('PUBLIC', 'PRIVATE')");
                table.HasCheckConstraint(
                    "CK_polls_question_not_blank",
                    "char_length(btrim(\"question\")) > 0");
                table.HasCheckConstraint(
                    "CK_polls_share_code_not_blank",
                    "char_length(btrim(\"share_code\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("id");

            builder.Property(x => x.CreatorId)
                .HasColumnName("created_by")
                .IsRequired();

            builder.Property(x => x.ShareCode)
                .HasColumnName("share_code")
                .HasMaxLength(ShareCodeGenerator.MaxLength)
                .IsRequired();

            builder.HasIndex(x => x.ShareCode)
                .IsUnique()
                .HasDatabaseName("IX_polls_share_code");

            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.Question)
                .HasColumnName("question")
                .HasColumnType("text")
                .IsRequired();

            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue(PollStatus.Draft);

            builder.Property(x => x.Visibility)
                .HasColumnName("visibility")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired()
                .HasDefaultValue(PollVisibility.Public);

            builder.Property(x => x.ExpiresAt)
                .HasColumnName("expires_at");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.CreatorId, x.CreatedAt })
                .IsDescending(false, true)
                .HasDatabaseName("IX_polls_created_by_created_at");

            builder.HasIndex(x => x.CreatedAt)
                .IsDescending()
                .HasFilter("\"status\" = 'ACTIVE' AND \"visibility\" = 'PUBLIC'")
                .HasDatabaseName("IX_polls_active_public_created_at");

            builder.HasIndex(x => x.ExpiresAt)
                .HasFilter("\"expires_at\" IS NOT NULL")
                .HasDatabaseName("IX_polls_expires_at");

            builder.HasOne(x => x.Creator)
                .WithMany(x => x.Polls)
                .HasForeignKey(x => x.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(x => x.Options)
                .WithOne(x => x.Poll)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Media)
                .WithOne(x => x.Poll)
                .HasForeignKey<PollMedia>(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.Votes)
                .WithOne(x => x.Poll)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
