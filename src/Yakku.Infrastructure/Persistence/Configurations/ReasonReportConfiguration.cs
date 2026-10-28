using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class ReasonReportConfiguration : IEntityTypeConfiguration<ReasonReport>
    {
        public void Configure(EntityTypeBuilder<ReasonReport> builder)
        {
            builder.ToTable("reason_reports", table =>
            {
                table.HasCheckConstraint(
                    "CK_reason_reports_content_type",
                    "\"content_type\" IN ('POLL', 'CHECK')");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.ReporterId).HasColumnName("reporter_id").IsRequired();
            builder.Property(x => x.ContentType)
                .HasColumnName("content_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.ContentId).HasColumnName("content_id").IsRequired();
            builder.Property(x => x.VoteId).HasColumnName("vote_id").IsRequired();
            builder.Property(x => x.OptionId).HasColumnName("option_id");
            builder.Property(x => x.ReasonText).HasColumnName("reason_text").HasColumnType("text").IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            // One report per reason: reporting again is a no-op.
            builder.HasIndex(x => x.VoteId).IsUnique().HasDatabaseName("IX_reason_reports_vote_id");
            builder.HasIndex(x => x.ReporterId).HasDatabaseName("IX_reason_reports_reporter_id");
            builder.HasIndex(x => new { x.ContentType, x.ContentId }).HasDatabaseName("IX_reason_reports_content");
            builder.HasIndex(x => x.CreatedAt).HasDatabaseName("IX_reason_reports_created_at");

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.ReporterId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
