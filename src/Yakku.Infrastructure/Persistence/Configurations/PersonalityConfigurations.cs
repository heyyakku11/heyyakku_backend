using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PersonalityTraitConfiguration : IEntityTypeConfiguration<PersonalityTrait>
    {
        public void Configure(EntityTypeBuilder<PersonalityTrait> builder)
        {
            builder.ToTable("personality_traits");
            builder.HasKey(x => x.Id);

            // Stable ids: the rows are seeded by the migration with fixed values.
            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
            builder.Property(x => x.DisplayOrder).HasColumnName("display_order").IsRequired();

            builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("IX_personality_traits_name");
            builder.HasIndex(x => x.DisplayOrder).HasDatabaseName("IX_personality_traits_display_order");
        }
    }

    internal class PersonalityCardConfiguration : IEntityTypeConfiguration<PersonalityCard>
    {
        public const string OneCardPerUserIndex = "IX_personality_cards_user_id";
        public const string ShareCodeIndex = "IX_personality_cards_share_code";

        public void Configure(EntityTypeBuilder<PersonalityCard> builder)
        {
            builder.ToTable("personality_cards", table =>
            {
                table.HasCheckConstraint(
                    "CK_personality_cards_status",
                    "\"status\" IN ('Draft', 'Collecting', 'Completed')");
                table.HasCheckConstraint(
                    "CK_personality_cards_required_votes_positive",
                    "\"required_votes\" > 0");
                table.HasCheckConstraint(
                    "CK_personality_cards_total_votes_in_range",
                    "\"total_votes\" >= 0 AND \"total_votes\" <= \"required_votes\"");
                table.HasCheckConstraint(
                    "CK_personality_cards_completed_at_matches_status",
                    "(\"status\" = 'Completed') = (\"completed_at\" IS NOT NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.ShareCode)
                .HasColumnName("share_code")
                .HasMaxLength(ShareCodeGenerator.MaxLength)
                .IsRequired();
            builder.Property(x => x.Status)
                .HasColumnName("status")
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired()
                .HasDefaultValue(PersonalityCardStatus.Draft);
            builder.Property(x => x.RequiredVotes).HasColumnName("required_votes").IsRequired().HasDefaultValue(PersonalityCard.DefaultRequiredVotes);
            builder.Property(x => x.TotalVotes).HasColumnName("total_votes").IsRequired().HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("now()");
            builder.Property(x => x.CompletedAt).HasColumnName("completed_at");

            // One card per user, enforced by the database.
            builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName(OneCardPerUserIndex);

            builder.HasIndex(x => x.ShareCode).IsUnique().HasDatabaseName(ShareCodeIndex);

            builder.HasOne(x => x.User)
                .WithOne(x => x.PersonalityCard)
                .HasForeignKey<PersonalityCard>(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal class PersonalityCardSelfTraitConfiguration : IEntityTypeConfiguration<PersonalityCardSelfTrait>
    {
        public void Configure(EntityTypeBuilder<PersonalityCardSelfTrait> builder)
        {
            builder.ToTable("personality_card_self_traits");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.PersonalityCardId).HasColumnName("personality_card_id").IsRequired();
            builder.Property(x => x.TraitId).HasColumnName("trait_id").IsRequired();

            builder.HasIndex(x => new { x.PersonalityCardId, x.TraitId })
                .IsUnique()
                .HasDatabaseName("IX_personality_card_self_traits_card_id_trait_id");
            builder.HasIndex(x => x.TraitId).HasDatabaseName("IX_personality_card_self_traits_trait_id");

            builder.HasOne(x => x.PersonalityCard)
                .WithMany(x => x.SelfTraits)
                .HasForeignKey(x => x.PersonalityCardId)
                .OnDelete(DeleteBehavior.Cascade);

            // Traits are lookup data: never deleted while referenced.
            builder.HasOne(x => x.Trait)
                .WithMany()
                .HasForeignKey(x => x.TraitId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal class PersonalityVoteConfiguration : IEntityTypeConfiguration<PersonalityVote>
    {
        public const string OneVotePerCardIndex = "IX_personality_votes_card_id_voter_user_id";
        public const string OneGuestVotePerCardIndex = "IX_personality_votes_card_id_voter_guest_id";

        public void Configure(EntityTypeBuilder<PersonalityVote> builder)
        {
            builder.ToTable("personality_votes");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.PersonalityCardId).HasColumnName("personality_card_id").IsRequired();
            builder.Property(x => x.VoterUserId).HasColumnName("voter_user_id");
            builder.Property(x => x.VoterGuestId).HasColumnName("voter_guest_id");

            // Exactly one kind of voter per vote.
            builder.ToTable(t => t.HasCheckConstraint(
                "CK_personality_votes_one_voter",
                "(\"voter_user_id\" IS NOT NULL AND \"voter_guest_id\" IS NULL) OR (\"voter_user_id\" IS NULL AND \"voter_guest_id\" IS NOT NULL)"));
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            // One vote per user per card, enforced by the database.
            builder.HasIndex(x => new { x.PersonalityCardId, x.VoterUserId })
                .IsUnique()
                .HasFilter("\"voter_user_id\" IS NOT NULL")
                .HasDatabaseName(OneVotePerCardIndex);
            builder.HasIndex(x => new { x.PersonalityCardId, x.VoterGuestId })
                .IsUnique()
                .HasFilter("\"voter_guest_id\" IS NOT NULL")
                .HasDatabaseName(OneGuestVotePerCardIndex);
            builder.HasIndex(x => x.VoterGuestId)
                .HasFilter("\"voter_guest_id\" IS NOT NULL")
                .HasDatabaseName("IX_personality_votes_voter_guest_id");
            builder.HasIndex(x => x.VoterUserId).HasDatabaseName("IX_personality_votes_voter_user_id");

            builder.HasOne(x => x.PersonalityCard)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.PersonalityCardId)
                .OnDelete(DeleteBehavior.Cascade);

            // Account purge removes the voter's votes explicitly (and fixes the card counters) before the user row.
            builder.HasOne(x => x.Voter)
                .WithMany()
                .HasForeignKey(x => x.VoterUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.VoterGuest)
                .WithMany()
                .HasForeignKey(x => x.VoterGuestId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal class PersonalityVoteTraitConfiguration : IEntityTypeConfiguration<PersonalityVoteTrait>
    {
        public void Configure(EntityTypeBuilder<PersonalityVoteTrait> builder)
        {
            builder.ToTable("personality_vote_traits");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.PersonalityVoteId).HasColumnName("personality_vote_id").IsRequired();
            builder.Property(x => x.TraitId).HasColumnName("trait_id").IsRequired();

            builder.HasIndex(x => new { x.PersonalityVoteId, x.TraitId })
                .IsUnique()
                .HasDatabaseName("IX_personality_vote_traits_vote_id_trait_id");
            // Serves the per-trait counts of the results query.
            builder.HasIndex(x => x.TraitId).HasDatabaseName("IX_personality_vote_traits_trait_id");

            builder.HasOne(x => x.PersonalityVote)
                .WithMany(x => x.Traits)
                .HasForeignKey(x => x.PersonalityVoteId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Trait)
                .WithMany()
                .HasForeignKey(x => x.TraitId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
