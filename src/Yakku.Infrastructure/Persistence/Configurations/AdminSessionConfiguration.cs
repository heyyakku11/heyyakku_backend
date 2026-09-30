using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class AdminSessionConfiguration : IEntityTypeConfiguration<AdminSession>
    {
        public void Configure(EntityTypeBuilder<AdminSession> builder)
        {
            builder.ToTable("AdminSessions");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AdminId)
                .IsRequired();

            builder.Property(x => x.RefreshTokenHash)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(x => x.IpAddress)
                .HasColumnType("inet");

            builder.Property(x => x.UserAgent)
                .HasColumnType("text");

            builder.HasIndex(x => x.RefreshTokenHash)
                .IsUnique()
                .HasDatabaseName("IX_AdminSessions_RefreshTokenHash");

            builder.HasIndex(x => x.AdminId)
                .HasDatabaseName("IX_AdminSessions_AdminId");

            builder.HasOne(x => x.Admin)
                .WithMany(x => x.Sessions)
                .HasForeignKey(x => x.AdminId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
