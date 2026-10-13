using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollOptionConfiguration : IEntityTypeConfiguration<PollOption>
    {
        public void Configure(EntityTypeBuilder<PollOption> builder)
        {
            builder.ToTable("poll_options", table =>
            {
                table.HasCheckConstraint(
                    "CK_poll_options_text_not_blank",
                    "char_length(btrim(\"option_text\")) > 0");
                table.HasCheckConstraint(
                    "CK_poll_options_display_order_positive",
                    "\"display_order\" > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .HasColumnName("id");

            builder.Property(x => x.PollId)
                .HasColumnName("poll_id")
                .IsRequired();

            builder.Property(x => x.Text)
                .HasColumnName("option_text")
                .HasMaxLength(500)
                .IsRequired();

            builder.Property(x => x.SortOrder)
                .HasColumnName("display_order")
                .IsRequired();

            builder.Property(x => x.CreatedAt)
                .HasColumnName("created_at")
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired()
                .HasDefaultValueSql("now()");

            builder.HasAlternateKey(x => new { x.Id, x.PollId })
                .HasName("AK_poll_options_id_poll_id");

            builder.HasIndex(x => new { x.PollId, x.SortOrder })
                .IsUnique()
                .HasDatabaseName("IX_poll_options_poll_id_display_order");

            builder.HasIndex(x => new { x.PollId, x.Text })
                .IsUnique()
                .HasDatabaseName("IX_poll_options_poll_id_option_text");
        }
    }
}
