using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
    {
        public void Configure(EntityTypeBuilder<PollOption> builder)
        {
            builder.ToTable("PollOptions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Text)
                .HasMaxLength(500);

            builder.Property(x => x.SortOrder)
                .IsRequired();

            builder.Property(x => x.VoteCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.HasIndex(x => new { x.PollId, x.SortOrder })
                .IsUnique()
                .HasDatabaseName("IX_PollOptions_PollId_SortOrder");

            builder.HasIndex(x => x.PollId)
                .HasDatabaseName("IX_PollOptions_PollId");

            builder.HasOne(x => x.Image)
                .WithMany(x => x.PollOptions)
                .HasForeignKey(x => x.ImageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
