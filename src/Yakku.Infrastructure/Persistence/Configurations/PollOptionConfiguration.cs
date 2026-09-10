using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollOptionConfiguration : IEntityTypeConfiguration<PollOptions>
    {
        public void Configure(EntityTypeBuilder<PollOptions> builder)
        {
            builder.ToTable("PollOptions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Text)
                .HasMaxLength(500);

            builder.Property(x => x.SortOrder)
                .IsRequired();

            builder.HasIndex(x => new { x.PollId, x.SortOrder })
                .IsUnique()
                .HasDatabaseName("IX_PollOptions_PollId_SortOrder");

            builder.HasOne(x => x.Image)
                .WithMany(x => x.PollOptions)
                .HasForeignKey(x => x.ImageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
