using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    /// <summary>The one table that holds the themes of every kind of card (checks, just-for-fun cards, and later kinds).</summary>
    internal class CardThemeConfiguration : IEntityTypeConfiguration<CardTheme>
    {
        public void Configure(EntityTypeBuilder<CardTheme> builder)
        {
            builder.ToTable("card_themes", table =>
            {
                table.HasCheckConstraint("CK_card_themes_background_color", "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                table.HasCheckConstraint("CK_card_themes_label_color", "\"label_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                table.HasCheckConstraint("CK_card_themes_name_not_blank", "char_length(btrim(\"name\")) > 0");
                table.HasCheckConstraint("CK_card_themes_tag_not_blank", "\"tag\" IS NULL OR char_length(btrim(\"tag\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Kind)
                .HasColumnName("kind")
                .HasUppercaseEnumConversion()
                .HasMaxLength(20)
                .IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.BackgroundColor).HasColumnName("background_color").HasMaxLength(7).IsRequired();
            builder.Property(x => x.LabelColor).HasColumnName("label_color").HasMaxLength(7).IsRequired();
            builder.Property(x => x.Tag).HasColumnName("tag").HasMaxLength(100);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");

            // One theme per name within a kind (the repository also rejects names that differ only by case).
            builder.HasIndex(x => new { x.Kind, x.Name }).IsUnique().HasDatabaseName("IX_card_themes_kind_name");
        }
    }
}
