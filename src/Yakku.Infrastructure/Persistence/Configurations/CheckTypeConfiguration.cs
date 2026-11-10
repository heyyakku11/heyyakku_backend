using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class CheckTypeConfiguration : IEntityTypeConfiguration<CheckType>
    {
        public void Configure(EntityTypeBuilder<CheckType> builder)
        {
            builder.ToTable("check_types", table =>
            {
                table.HasCheckConstraint("CK_check_types_background_color", "\"background_color\" ~ '^#[0-9A-Fa-f]{6}$'");
                table.HasCheckConstraint("CK_check_types_name_not_blank", "char_length(btrim(\"name\")) > 0");
                table.HasCheckConstraint("CK_check_types_sticker_not_blank", "\"sticker\" IS NULL OR char_length(btrim(\"sticker\")) > 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            builder.Property(x => x.BackgroundColor).HasColumnName("background_color").HasMaxLength(7).IsRequired();
            builder.Property(x => x.Sticker).HasColumnName("sticker").HasMaxLength(100);

            // One theme per name (the repository also rejects names that differ only by case).
            builder.HasIndex(x => x.Name).IsUnique().HasDatabaseName("IX_check_types_name");
        }
    }
}
