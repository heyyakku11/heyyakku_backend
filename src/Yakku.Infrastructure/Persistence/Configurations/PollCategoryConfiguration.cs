using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollCategoryConfiguration : IEntityTypeConfiguration<PollCategory>
    {
        public void Configure(EntityTypeBuilder<PollCategory> builder)
        {
            builder.ToTable("PollCategories");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.PollId)
                .IsRequired();

            builder.Property(x => x.CategoryId)
                .IsRequired();

            builder.Property(x => x.AddedAt)
                .IsRequired();

            builder.HasIndex(x => new { x.PollId, x.CategoryId })
                .IsUnique()
                .HasDatabaseName("IX_PollCategories_PollId_CategoryId");

            builder.HasIndex(x => x.CategoryId)
                .HasDatabaseName("IX_PollCategories_CategoryId");

            builder.HasOne(x => x.Poll)
                .WithMany(x => x.PollCategories)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Category)
                .WithMany(x => x.PollCategories)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
