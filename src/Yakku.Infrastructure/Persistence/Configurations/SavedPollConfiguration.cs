using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class SavedPollConfiguration : IEntityTypeConfiguration<SavedPoll>
    {
        public void Configure(EntityTypeBuilder<SavedPoll> builder)
        {
            builder.ToTable("SavedPolls");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.PollId)
                .IsRequired();

            builder.Property(x => x.SavedAt)
                .IsRequired();

            builder.HasIndex(x => new { x.UserId, x.PollId })
                .IsUnique()
                .HasDatabaseName("IX_SavedPolls_UserId_PollId");

            builder.HasIndex(x => x.PollId)
                .HasDatabaseName("IX_SavedPolls_PollId");

            builder.HasOne(x => x.User)
                .WithMany(x => x.SavedPolls)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Poll)
                .WithMany(x => x.SavedPolls)
                .HasForeignKey(x => x.PollId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
