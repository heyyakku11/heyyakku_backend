using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollConfiguration : IEntityTypeConfiguration<Polls>
    {
        public void Configure(EntityTypeBuilder<Polls> builder)
        {
            builder.ToTable("Polls");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CreatorId)
                .IsRequired();

            builder.Property(x => x.ShareToken)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Question)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.OptionType)
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.HasIndex(x => x.CreatorId)
                .HasDatabaseName("IX_Polls_CreatorId");

            builder.HasIndex(x => x.CategoryId)
                .HasDatabaseName("IX_Polls_CategoryId");

            builder.HasIndex(x => x.Status)
                .HasDatabaseName("IX_Polls_Status");

            builder.HasIndex(x => x.ShareToken)
                .IsUnique()
                .HasDatabaseName("IX_Polls_ShareToken");

            builder.HasOne(x => x.Creator)
                .WithMany(x => x.Polls)
                .HasForeignKey(x => x.CreatorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.Category)
                .WithMany(x => x.Polls)
                .HasForeignKey(x => x.CategoryId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasMany(x => x.Options)
                .WithOne(x => x.Poll)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
