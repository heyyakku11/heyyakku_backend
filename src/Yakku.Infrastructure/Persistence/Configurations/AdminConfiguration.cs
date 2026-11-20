using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class AdminConfiguration : IEntityTypeConfiguration<Admin>
    {
        public void Configure(EntityTypeBuilder<Admin> builder)
        {
            builder.ToTable("admins");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(320);

            builder.Property(x => x.Role)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            builder.Property(x => x.Status)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired()
                .HasDefaultValue(AdminStatus.Active);

            builder.Property(x => x.IsVerified)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasIndex(x => x.Email)
                .IsUnique()
                .HasDatabaseName("IX_Admins_Email");

            builder.HasOne(x => x.Password)
                .WithOne(x => x.Admin)
                .HasForeignKey<AdminPassword>(x => x.AdminId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal class AdminPasswordConfiguration : IEntityTypeConfiguration<AdminPassword>
    {
        public void Configure(EntityTypeBuilder<AdminPassword> builder)
        {
            builder.ToTable("admin_passwords");
            builder.HasKey(x => x.AdminId);

            builder.Property(x => x.AdminId).HasColumnName("admin_id");
            builder.Property(x => x.PasswordHash)
                .HasColumnName("password_hash")
                .IsRequired()
                .HasMaxLength(255);
            builder.Property(x => x.UpdatedAt)
                .HasColumnName("updated_at")
                .IsRequired();
        }
    }
}
