using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Configurations
{
    internal class CommunityWaitlistConfiguration : IEntityTypeConfiguration<CommunityWaitlistEntry>
    {
        public void Configure(EntityTypeBuilder<CommunityWaitlistEntry> builder)
        {
            builder.ToTable("community_waitlist");
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("now()");
            builder.Property(x => x.NotifiedAt).HasColumnName("notified_at");

            // A user joins the queue once.
            builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("IX_community_waitlist_user_id");

            // Queue order, and finding who has not been told yet.
            builder.HasIndex(x => new { x.NotifiedAt, x.CreatedAt }).HasDatabaseName("IX_community_waitlist_notified_at_created_at");

            builder.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
