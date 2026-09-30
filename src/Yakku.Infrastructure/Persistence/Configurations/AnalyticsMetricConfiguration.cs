using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class AnalyticsMetricConfiguration : IEntityTypeConfiguration<AnalyticsMetric>
    {
        public void Configure(EntityTypeBuilder<AnalyticsMetric> builder)
        {
            builder.ToTable("AnalyticsMetrics");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.MetricType)
                .HasConversion<string>()
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(x => x.MetricDate)
                .HasColumnType("date")
                .IsRequired();

            builder.Property(x => x.Value)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.Metadata)
                .HasColumnType("jsonb");

            builder.HasIndex(x => new { x.MetricType, x.MetricDate })
                .HasDatabaseName("IX_AnalyticsMetrics_MetricType_MetricDate");

            builder.HasIndex(x => x.CategoryId)
                .HasDatabaseName("IX_AnalyticsMetrics_CategoryId");

            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_AnalyticsMetrics_UserId");

            builder.HasOne(x => x.Category)
                .WithMany(x => x.AnalyticsMetrics)
                .HasForeignKey(x => x.CategoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.User)
                .WithMany(x => x.AnalyticsMetrics)
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
