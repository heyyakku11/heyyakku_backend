using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollAnalyticsConfiguration : IEntityTypeConfiguration<PollAnalytics>
    {
        public void Configure(EntityTypeBuilder<PollAnalytics> builder)
        {
            builder.ToTable("PollAnalytics");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PollId)
                .IsRequired();

            builder.Property(x => x.TotalVotes)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.UniqueVoters)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.CommentCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.CompletionRate)
                .HasPrecision(5, 2);

            builder.Property(x => x.GuestVotesCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.UserVotesCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.Views)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.Shares)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.ReportCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.HasIndex(x => x.PollId)
                .IsUnique()
                .HasDatabaseName("IX_PollAnalytics_PollId");

            builder.HasOne(x => x.Poll)
                .WithOne(x => x.PollAnalytics)
                .HasForeignKey<PollAnalytics>(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
