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
                .IsRequired(false);

            builder.HasOne<User>()
                .WithMany()
                .HasForeignKey(x => x.CreatorId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(x => x.CreatorId)
                .HasDatabaseName("IX_Polls_CreatorId");

            builder.Property(x => x.Question)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32);

            builder.HasMany(x => x.Options)
                .WithOne(x => x.Poll)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
