using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class CategoryConfiguration : IEntityTypeConfiguration<Category>
    {
        public void Configure(EntityTypeBuilder<Category> builder)
        {
            builder.ToTable("Categories");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Slug)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Icon)
                .HasMaxLength(200);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.HasIndex(x => x.Name)
                .IsUnique()
                .HasDatabaseName("IX_Categories_Name");

            builder.HasIndex(x => x.Slug)
                .IsUnique()
                .HasDatabaseName("IX_Categories_Slug");

            builder.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_Categories_IsActive");

            builder.HasIndex(x => x.Icon)
                .IsUnique()
                .HasFilter("\"Icon\" IS NOT NULL")
                .HasDatabaseName("IX_Categories_Icon");
        }
    }
}
