using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class MediaUploadSessionConfiguration : IEntityTypeConfiguration<MediaUploadSession>
    {
        public void Configure(EntityTypeBuilder<MediaUploadSession> builder)
        {
            builder.ToTable("media_upload_sessions", table =>
            {
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_media_type",
                    "\"media_type\" IN ('IMAGE', 'VIDEO', 'AUDIO')");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_status",
                    "\"status\" IN ('PENDING', 'UPLOADING', 'UPLOADED', 'ATTACHED', 'CANCELLED', 'FAILED', 'EXPIRED')");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_bucket_not_blank",
                    "char_length(btrim(\"bucket_name\")) > 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_object_key_not_blank",
                    "char_length(btrim(\"object_key\")) > 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_mime_type_not_blank",
                    "char_length(btrim(\"mime_type\")) > 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_selection_version_positive",
                    "\"selection_version\" > 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_expected_size_positive",
                    "\"expected_file_size\" IS NULL OR \"expected_file_size\" > 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_actual_size_positive",
                    "\"actual_file_size\" IS NULL OR \"actual_file_size\" > 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_duration_non_negative",
                    "\"duration_ms\" IS NULL OR \"duration_ms\" >= 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_failure_reason_not_blank",
                    "\"failure_reason\" IS NULL OR char_length(btrim(\"failure_reason\")) > 0");
                table.HasCheckConstraint(
                    "CK_media_upload_sessions_owner_xor",
                    "(\"user_id\" IS NOT NULL AND \"guest_id\" IS NULL) OR (\"user_id\" IS NULL AND \"guest_id\" IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("id");

            builder.Property(x => x.UserId)
                .HasColumnName("user_id");

            builder.Property(x => x.GuestId)
                .HasColumnName("guest_id");

            builder.Property(x => x.PollId)
                .HasColumnName("poll_id");

            builder.Property(x => x.MediaType)
                .HasColumnName("media_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.BucketName)
                .HasColumnName("bucket_name")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.ObjectKey)
                .HasColumnName("object_key")
                .HasColumnType("text")
                .IsRequired();

            builder.Property(x => x.MimeType)
                .HasColumnName("mime_type")
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(x => x.ExpectedFileSize)
                .HasColumnName("expected_file_size");

            builder.Property(x => x.ActualFileSize)
                .HasColumnName("actual_file_size");

            builder.Property(x => x.DurationMs)
                .HasColumnName("duration_ms");

            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();

            builder.Property(x => x.SelectionVersion)
                .HasColumnName("selection_version")
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            builder.Property(x => x.ExpiresAt)
                .HasColumnName("expires_at")
                .IsRequired();

            builder.Property(x => x.AttachedAt)
                .HasColumnName("attached_at");

            builder.Property(x => x.FailureReason)
                .HasColumnName("failure_reason")
                .HasMaxLength(MediaUploadSession.FailureReasonMaxLength);

            builder.Property(x => x.PurgedAt)
                .HasColumnName("purged_at");

            builder.HasIndex(x => x.ObjectKey)
                .IsUnique()
                .HasDatabaseName("IX_media_upload_sessions_object_key");

            builder.HasIndex(x => new { x.UserId, x.Status, x.ExpiresAt })
                .HasDatabaseName("IX_media_upload_sessions_user_id_status_expires_at");

            builder.HasIndex(x => new { x.GuestId, x.Status })
                .HasFilter("\"guest_id\" IS NOT NULL")
                .HasDatabaseName("IX_media_upload_sessions_guest_id_status");

            builder.HasIndex(x => x.PollId)
                .HasFilter("\"poll_id\" IS NOT NULL")
                .HasDatabaseName("IX_media_upload_sessions_poll_id");

            builder.HasOne(x => x.User)
                .WithMany(x => x.UploadSessions)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Guest)
                .WithMany()
                .HasForeignKey(x => x.GuestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Poll)
                .WithMany(x => x.UploadSessions)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
