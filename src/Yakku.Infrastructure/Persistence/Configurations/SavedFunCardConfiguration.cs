using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class SavedFunCardConfiguration : IEntityTypeConfiguration<SavedFunCard>
    {
        public void Configure(EntityTypeBuilder<SavedFunCard> builder)
        {
            builder.ToTable("saved_fun_cards");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId).IsRequired();
            builder.Property(x => x.CheckId).IsRequired();
            builder.Property(x => x.SavedAt).IsRequired();

            builder.HasIndex(x => new { x.UserId, x.CheckId })
                .IsUnique()
                .HasDatabaseName("IX_SavedFunCards_UserId_CheckId");

            builder.HasIndex(x => x.CheckId)
                .HasDatabaseName("IX_SavedFunCards_CheckId");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Check)
                .WithMany()
                .HasForeignKey(x => x.CheckId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
