using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class VoteConfiguration : IEntityTypeConfiguration<Vote>
    {
        public void Configure(EntityTypeBuilder<Vote> builder)
        {
            builder.ToTable("Votes", table =>
            {
                table.HasCheckConstraint(
                    "CK_Votes_OneVoter",
                    "(\"UserId\" IS NOT NULL AND \"GuestId\" IS NULL) OR (\"UserId\" IS NULL AND \"GuestId\" IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PollId)
                .IsRequired();

            builder.Property(x => x.CustomOptionText)
                .HasMaxLength(500);

            builder.Property(x => x.Reason)
                .HasMaxLength(1000);

            builder.HasIndex(x => x.PollId)
                .HasDatabaseName("IX_Votes_PollId");

            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_Votes_UserId");

            builder.HasIndex(x => x.GuestId)
                .HasDatabaseName("IX_Votes_GuestId");

            builder.HasIndex(x => x.PollOptionId)
                .HasDatabaseName("IX_Votes_PollOptionId");

            builder.HasIndex(x => new { x.PollId, x.UserId })
                .IsUnique()
                .HasFilter("\"UserId\" IS NOT NULL")
                .HasDatabaseName("IX_Votes_PollId_UserId");

            builder.HasIndex(x => new { x.PollId, x.GuestId })
                .IsUnique()
                .HasFilter("\"GuestId\" IS NOT NULL")
                .HasDatabaseName("IX_Votes_PollId_GuestId");

            builder.HasOne(x => x.Poll)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.User)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.UserId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Guest)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.GuestId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.PollOption)
                .WithMany()
                .HasForeignKey(x => x.PollOptionId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(x => x.Image)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.ImageId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
