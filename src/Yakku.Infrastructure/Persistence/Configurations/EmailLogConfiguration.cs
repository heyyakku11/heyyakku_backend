using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class EmailLogConfiguration : IEntityTypeConfiguration<EmailLog>
    {
        public void Configure(EntityTypeBuilder<EmailLog> builder)
        {
            builder.ToTable("EmailLogs");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.RecipientEmail)
                .IsRequired()
                .HasMaxLength(320);

            builder.Property(x => x.EmailType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Provider)
                .IsRequired()
                .HasMaxLength(64);

            builder.Property(x => x.ProviderMessageId)
                .HasMaxLength(128);

            builder.Property(x => x.ErrorCode)
                .HasMaxLength(64);

            builder.Property(x => x.ErrorMessage)
                .HasMaxLength(EmailLog.MaxErrorMessageLength);

            builder.Property(x => x.AttemptCount)
                .IsRequired();

            builder.Property(x => x.MaxAttempts)
                .IsRequired();

            builder.HasIndex(x => x.Status)
                .HasDatabaseName("IX_EmailLogs_Status");

            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_EmailLogs_CreatedAt");

            builder.HasIndex(x => x.ReferenceId)
                .IsUnique()
                .HasDatabaseName("IX_EmailLogs_ReferenceId");

            builder.HasIndex(x => x.RecipientEmail)
                .HasDatabaseName("IX_EmailLogs_RecipientEmail");

            builder.HasIndex(x => new { x.Status, x.QueuedAt })
                .HasDatabaseName("IX_EmailLogs_Status_QueuedAt");

            builder.HasOne(x => x.User)
                .WithMany(x => x.EmailLogs)
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
