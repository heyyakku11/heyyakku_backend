using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    /// <summary>The one table of cards users saved: polls and fun cards, told apart by card_type.</summary>
    internal class SavedCardConfiguration : IEntityTypeConfiguration<SavedCard>
    {
        public const string PollUniqueIndex = "IX_saved_cards_user_poll";
        public const string FunCardUniqueIndex = "IX_saved_cards_user_funcard";

        public void Configure(EntityTypeBuilder<SavedCard> builder)
        {
            builder.ToTable("saved_cards", table =>
            {
                // A poll row points at a poll and only a poll; a fun card row at a check and only a check.
                table.HasCheckConstraint(
                    "CK_saved_cards_card_type_matches_target",
                    "(\"card_type\" = 'POLL' AND \"poll_id\" IS NOT NULL AND \"funcard_id\" IS NULL) " +
                    "OR (\"card_type\" = 'FUNCARD' AND \"funcard_id\" IS NOT NULL AND \"poll_id\" IS NULL)");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.CardType)
                .HasColumnName("card_type")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.PollId).HasColumnName("poll_id");
            builder.Property(x => x.FunCardId).HasColumnName("funcard_id");
            builder.Property(x => x.SavedAt).HasColumnName("saved_at").IsRequired();

            // A user saves a given card once.
            builder.HasIndex(x => new { x.UserId, x.PollId })
                .IsUnique()
                .HasFilter("\"poll_id\" IS NOT NULL")
                .HasDatabaseName(PollUniqueIndex);
            builder.HasIndex(x => new { x.UserId, x.FunCardId })
                .IsUnique()
                .HasFilter("\"funcard_id\" IS NOT NULL")
                .HasDatabaseName(FunCardUniqueIndex);

            // "My saved cards, newest first".
            builder.HasIndex(x => new { x.UserId, x.SavedAt })
                .HasDatabaseName("IX_saved_cards_user_saved_at");
            builder.HasIndex(x => x.PollId)
                .HasFilter("\"poll_id\" IS NOT NULL")
                .HasDatabaseName("IX_saved_cards_poll_id");
            builder.HasIndex(x => x.FunCardId)
                .HasFilter("\"funcard_id\" IS NOT NULL")
                .HasDatabaseName("IX_saved_cards_funcard_id");

            builder.HasOne(x => x.User)
                .WithMany(x => x.SavedCards)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Poll)
                .WithMany(x => x.SavedCards)
                .HasForeignKey(x => x.PollId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Check)
                .WithMany()
                .HasForeignKey(x => x.FunCardId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
