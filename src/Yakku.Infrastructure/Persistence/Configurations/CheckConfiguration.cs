using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class CheckConfiguration : IEntityTypeConfiguration<Check>
    {
        public void Configure(EntityTypeBuilder<Check> builder)
        {
            builder.ToTable("checks", table =>
            {
                table.HasCheckConstraint(
                    "CK_checks_background_color",
                    "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                table.HasCheckConstraint(
                    "CK_checks_live_week_start_monday",
                    "EXTRACT(ISODOW FROM \"live_week_start\") = 1");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.LiveWeekStart).HasColumnName("live_week_start").HasColumnType("date").IsRequired();
            builder.Property(x => x.SequenceNo).HasColumnName("sequence_no").IsRequired();
            builder.Property(x => x.Title).HasColumnName("title").HasMaxLength(100).IsRequired();
            builder.Property(x => x.Question).HasColumnName("question").HasMaxLength(500).IsRequired();
            builder.Property(x => x.BackgroundColor).HasColumnName("background_color").HasMaxLength(7).IsRequired();
            builder.Property(x => x.Sticker).HasColumnName("sticker").HasMaxLength(100).IsRequired();
            builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("now()");

            // sequence_no orders the checks that go live in the same week.
            builder.HasIndex(x => new { x.LiveWeekStart, x.SequenceNo })
                .IsUnique()
                .HasDatabaseName("IX_checks_live_week_start_sequence_no");

            builder.HasMany(x => x.Options)
                .WithOne(x => x.Check)
                .HasForeignKey(x => x.CheckId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal class CheckOptionConfiguration : IEntityTypeConfiguration<CheckOption>
    {
        public void Configure(EntityTypeBuilder<CheckOption> builder)
        {
            builder.ToTable("check_options");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.CheckId).HasColumnName("check_id").IsRequired();
            builder.Property(x => x.Text).HasColumnName("text").HasMaxLength(200).IsRequired();
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();

            builder.HasIndex(x => new { x.CheckId, x.SortOrder })
                .IsUnique()
                .HasDatabaseName("IX_check_options_check_id_sort_order");

            // Alternate key lets votes reference (option, check) so an option must belong to the voted check.
            builder.HasAlternateKey(x => new { x.Id, x.CheckId });
        }
    }

    internal class UserCheckConfiguration : IEntityTypeConfiguration<UserCheck>
    {
        public void Configure(EntityTypeBuilder<UserCheck> builder)
        {
            builder.ToTable("user_checks");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.CheckId).HasColumnName("check_id").IsRequired();
            builder.Property(x => x.ShareCode).HasColumnName("share_code").HasMaxLength(32).IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.UserId, x.CheckId })
                .IsUnique()
                .HasDatabaseName("IX_user_checks_user_id_check_id");
            builder.HasIndex(x => x.CheckId).HasDatabaseName("IX_user_checks_check_id");
            builder.HasIndex(x => x.ShareCode).IsUnique().HasDatabaseName("IX_user_checks_share_code");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Check)
                .WithMany(x => x.UserChecks)
                .HasForeignKey(x => x.CheckId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }

    internal class UserCheckVoteConfiguration : IEntityTypeConfiguration<UserCheckVote>
    {
        public void Configure(EntityTypeBuilder<UserCheckVote> builder)
        {
            builder.ToTable("user_check_votes", table =>
            {
                table.HasCheckConstraint(
                    "CK_user_check_votes_reason_not_blank",
                    "\"reason\" IS NULL OR char_length(btrim(\"reason\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserCheckId).HasColumnName("user_check_id").IsRequired();
            builder.Property(x => x.CheckId).HasColumnName("check_id").IsRequired();
            builder.Property(x => x.CheckOptionId).HasColumnName("check_option_id").IsRequired();
            builder.Property(x => x.VoterId).HasColumnName("voter_id").IsRequired();
            builder.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            builder.HasIndex(x => new { x.UserCheckId, x.VoterId })
                .IsUnique()
                .HasDatabaseName("IX_user_check_votes_user_check_id_voter_id");
            builder.HasIndex(x => x.VoterId).HasDatabaseName("IX_user_check_votes_voter_id");
            builder.HasIndex(x => x.CheckOptionId).HasDatabaseName("IX_user_check_votes_check_option_id");

            builder.HasOne(x => x.UserCheck)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => x.UserCheckId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Voter)
                .WithMany()
                .HasForeignKey(x => x.VoterId)
                .OnDelete(DeleteBehavior.Cascade);

            // (check_option_id, check_id) guarantees the option belongs to the same check as the shared item.
            builder.HasOne(x => x.CheckOption)
                .WithMany(x => x.Votes)
                .HasForeignKey(x => new { x.CheckOptionId, x.CheckId })
                .HasPrincipalKey(x => new { x.Id, x.CheckId })
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
