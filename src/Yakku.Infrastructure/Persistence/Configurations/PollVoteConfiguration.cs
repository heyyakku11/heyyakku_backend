using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollVoteConfiguration : IEntityTypeConfiguration<PollVote>
    {
        public void Configure(EntityTypeBuilder<PollVote> builder)
        {
            builder.ToTable("poll_votes", table =>
            {
                table.HasCheckConstraint(
                    "CK_poll_votes_one_choice",
                    """
                    ("poll_option_id" IS NOT NULL AND "custom_answer" IS NULL)
                    OR
                    ("poll_option_id" IS NULL AND "custom_answer" IS NOT NULL)
                    """);
                table.HasCheckConstraint(
                    "CK_poll_votes_custom_answer_not_blank",
                    "\"custom_answer\" IS NULL OR char_length(btrim(\"custom_answer\")) > 0");
                table.HasCheckConstraint(
                    "CK_poll_votes_reason_not_blank",
                    "\"reason\" IS NULL OR char_length(btrim(\"reason\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("id");

            builder.Property(x => x.PollId)
                .HasColumnName("poll_id")
                .IsRequired();

            builder.Property(x => x.VoterId)
                .HasColumnName("voter_id")
                .IsRequired();

            builder.Property(x => x.PollOptionId)
                .HasColumnName("poll_option_id");

            builder.Property(x => x.CustomAnswer)
                .HasColumnName("custom_answer")
                .HasColumnType("text");

            builder.Property(x => x.Reason)
                .HasColumnName("reason")
                .HasColumnType("text");

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.PollId, x.VoterId })
                .IsUnique()
                .HasDatabaseName("IX_poll_votes_poll_id_voter_id");

            builder.HasIndex(x => x.VoterId)
                .HasDatabaseName("IX_poll_votes_voter_id");

            builder.HasIndex(x => x.PollOptionId)
                .HasFilter("\"poll_option_id\" IS NOT NULL")
                .HasDatabaseName("IX_poll_votes_poll_option_id");

            builder.HasOne(x => x.Voter)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.VoterId)
                .OnDelete(DeleteBehavior.Restrict);

            // poll_id is also the foreign key to polls. The composite key guarantees the
            // selected option belongs to this poll. PostgreSQL MATCH SIMPLE still allows
            // a null poll_option_id for custom-answer votes. The migration makes this
            // constraint DEFERRABLE so deleting a poll can cascade votes and options.
            builder.HasOne(x => x.PollOption)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => new { x.PollOptionId, x.PollId })
                .HasPrincipalKey(x => new { x.Id, x.PollId })
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
