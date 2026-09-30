using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class ReportConfiguration : IEntityTypeConfiguration<Report>
    {
        public void Configure(EntityTypeBuilder<Report> builder)
        {
            builder.ToTable("Reports", table =>
            {
                table.HasCheckConstraint(
                    "CK_Reports_OneReporter",
                    "(\"ReportedByUserId\" IS NOT NULL AND \"ReportedByGuestId\" IS NULL) OR (\"ReportedByUserId\" IS NULL AND \"ReportedByGuestId\" IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.ResourceType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.ResourceId)
                .IsRequired();

            builder.Property(x => x.Reason)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Description)
                .HasMaxLength(1000);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired()
                .HasDefaultValue(ReportStatus.Pending);

            builder.Property(x => x.ReviewNotes)
                .HasMaxLength(1000);

            builder.Property(x => x.Action)
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.HasIndex(x => x.ReportedByUserId)
                .HasDatabaseName("IX_Reports_ReportedByUserId");

            builder.HasIndex(x => x.ReportedByGuestId)
                .HasDatabaseName("IX_Reports_ReportedByGuestId");

            builder.HasIndex(x => new { x.ResourceType, x.ResourceId })
                .HasDatabaseName("IX_Reports_ResourceType_ResourceId");

            builder.HasIndex(x => x.Status)
                .HasDatabaseName("IX_Reports_Status");

            builder.HasIndex(x => x.ReviewedBy)
                .HasDatabaseName("IX_Reports_ReviewedBy");

            builder.HasOne(x => x.ReportedByUser)
                .WithMany(x => x.Reports)
                .HasForeignKey(x => x.ReportedByUserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ReportedByGuest)
                .WithMany(x => x.Reports)
                .HasForeignKey(x => x.ReportedByGuestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.ReviewedByAdmin)
                .WithMany(x => x.ReviewedReports)
                .HasForeignKey(x => x.ReviewedBy)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
