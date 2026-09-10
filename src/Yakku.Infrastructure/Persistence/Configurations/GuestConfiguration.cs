using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class GuestConfiguration : IEntityTypeConfiguration<Guest>
    {
        public void Configure(EntityTypeBuilder<Guest> builder)
        {
            builder.ToTable("Guests");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.GuestTokenHash)
                .IsRequired()
                .HasMaxLength(255);

            builder.HasIndex(x => x.GuestTokenHash)
                .IsUnique()
                .HasDatabaseName("IX_Guests_GuestTokenHash");
        }
    }
}
