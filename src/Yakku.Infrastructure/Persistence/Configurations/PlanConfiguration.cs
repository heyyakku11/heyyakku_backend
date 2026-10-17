using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PlanConfiguration : IEntityTypeConfiguration<Plan>
    {
        public void Configure(EntityTypeBuilder<Plan> builder)
        {
            builder.ToTable("plans", table =>
            {
                table.HasCheckConstraint("CK_plans_price_not_negative", "\"price_minor\" >= 0");
            });
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            builder.Property(x => x.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
            builder.Property(x => x.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
            builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
            builder.Property(x => x.PriceMinor).HasColumnName("price_minor").IsRequired().HasDefaultValue(0);
            builder.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(3).IsRequired().HasDefaultValue("INR");
            builder.Property(x => x.IsActive).HasColumnName("is_active").IsRequired().HasDefaultValue(true);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired().HasDefaultValueSql("now()");

            builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_plans_code");
            builder.HasIndex(x => x.SortOrder).IsUnique().HasDatabaseName("IX_plans_sort_order");

            builder.HasMany(x => x.Users)
                .WithOne(x => x.Plan)
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
