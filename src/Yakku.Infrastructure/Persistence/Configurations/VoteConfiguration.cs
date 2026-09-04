using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class VoteConfiguration : IEntityTypeConfiguration<Vote>
    {
        public void Configure(EntityTypeBuilder<Vote> builder)
        {
            builder.ToTable("Votes");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CustomOptionText)
                .HasMaxLength(200);

            builder.Property(x => x.Reason)
                .HasMaxLength(500);

            builder.HasIndex(x => new { x.GuestId, x.PollId })
                .IsUnique()
                .HasDatabaseName("IX_Votes_GuestId_PollId");

            builder.HasOne<Guest>()
                .WithMany()
                .HasForeignKey(x => x.GuestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Polls>()
                .WithMany()
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne<PollOptions>()
                .WithMany()
                .HasForeignKey(x => x.PollOptionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
