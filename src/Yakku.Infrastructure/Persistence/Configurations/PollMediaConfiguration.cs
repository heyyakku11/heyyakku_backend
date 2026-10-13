using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollMediaConfiguration : IEntityTypeConfiguration<PollMedia>
    {
        public void Configure(EntityTypeBuilder<PollMedia> builder)
        {
            builder.ToTable("poll_media", table =>
            {
                table.HasCheckConstraint(
                    "CK_poll_media_media_type",
                    "\"media_type\" IN ('IMAGE', 'VIDEO', 'AUDIO')");
                table.HasCheckConstraint(
                    "CK_poll_media_file_size_positive",
                    "\"file_size\" > 0");
                table.HasCheckConstraint(
                    "CK_poll_media_duration_positive",
                    "\"duration_ms\" IS NULL OR \"duration_ms\" > 0");
                table.HasCheckConstraint(
                    "CK_poll_media_bucket_not_blank",
                    "char_length(btrim(\"bucket_name\")) > 0");
                table.HasCheckConstraint(
                    "CK_poll_media_object_key_not_blank",
                    "char_length(btrim(\"object_key\")) > 0");
                table.HasCheckConstraint(
                    "CK_poll_media_mime_type_not_blank",
                    "char_length(btrim(\"mime_type\")) > 0");
                table.HasCheckConstraint(
                    "CK_poll_media_thumbnail_not_blank",
                    "\"thumbnail_object_key\" IS NULL OR char_length(btrim(\"thumbnail_object_key\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("id");

            builder.Property(x => x.PollId)
                .HasColumnName("poll_id")
                .IsRequired();

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

            builder.Property(x => x.FileSize)
                .HasColumnName("file_size")
                .IsRequired();

            builder.Property(x => x.DurationMs)
                .HasColumnName("duration_ms");

            builder.Property(x => x.ThumbnailObjectKey)
                .HasColumnName("thumbnail_object_key")
                .HasColumnType("text");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.HasIndex(x => x.PollId)
                .IsUnique()
                .HasDatabaseName("IX_poll_media_poll_id");
        }
    }
}
