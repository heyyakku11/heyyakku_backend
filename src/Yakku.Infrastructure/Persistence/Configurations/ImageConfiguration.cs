using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class ImageConfiguration : IEntityTypeConfiguration<Image>
    {
        public void Configure(EntityTypeBuilder<Image> builder)
        {
            builder.ToTable("Images");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Provider)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.PublicId)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(x => x.Url)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(x => x.SecureUrl)
                .IsRequired()
                .HasColumnType("text");

            builder.Property(x => x.ResourceType)
                .HasMaxLength(50);

            builder.Property(x => x.Format)
                .HasMaxLength(20);

            builder.HasIndex(x => x.PublicId)
                .HasDatabaseName("IX_Images_PublicId");
        }
    }
}
