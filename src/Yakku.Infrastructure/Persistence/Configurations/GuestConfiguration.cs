using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class GuestConfiguration : IEntityTypeConfiguration<Guest>
    {
        public void Configure(EntityTypeBuilder<Guest> builder)
        {
            builder.ToTable("guests");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.SessionTokenHash)
                .IsRequired()
                .HasMaxLength(255);

            builder.Property(x => x.ExpiresAt)
                .IsRequired();

            builder.Property(x => x.IdempotencyKey)
                .HasColumnName("idempotency_key")
                .HasMaxLength(Guest.MaxIdempotencyKeyLength);

            builder.Property(x => x.IpAddress)
                .HasColumnType("inet");

            builder.Property(x => x.UserAgent)
                .HasMaxLength(Guest.MaxUserAgentLength);

            builder.Property(x => x.BrowserName)
                .HasMaxLength(Guest.MaxBrowserNameLength);

            builder.Property(x => x.BrowserVersion)
                .HasMaxLength(Guest.MaxBrowserVersionLength);

            builder.Property(x => x.OsName)
                .HasMaxLength(Guest.MaxOsNameLength);

            builder.Property(x => x.DeviceType)
                .HasConversion<string>()
                .HasMaxLength(16);

            builder.Property(x => x.Language)
                .HasMaxLength(Guest.MaxLanguageLength);

            builder.Property(x => x.CountryCode)
                .HasMaxLength(Guest.CountryCodeLength)
                .IsFixedLength();

            builder.HasIndex(x => x.SessionTokenHash)
                .IsUnique()
                .HasDatabaseName("IX_Guests_SessionTokenHash");

            builder.HasIndex(x => x.ExpiresAt)
                .HasDatabaseName("IX_Guests_ExpiresAt");

            builder.HasIndex(x => x.IdempotencyKey)
                .IsUnique()
                .HasFilter("idempotency_key IS NOT NULL")
                .HasDatabaseName("IX_Guests_IdempotencyKey");
        }
    }
}
