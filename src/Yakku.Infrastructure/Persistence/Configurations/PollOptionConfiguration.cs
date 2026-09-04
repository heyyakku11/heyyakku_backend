using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class PollOptionConfiguration : IEntityTypeConfiguration<PollOptions>
    {
        public void Configure(EntityTypeBuilder<PollOptions> builder)
        {
            builder.ToTable("PollOptions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Text)
                .IsRequired()
                .HasMaxLength(200);

            builder.HasIndex(x => new { x.PollId, x.Position })
                .IsUnique();
        }
    }
}
