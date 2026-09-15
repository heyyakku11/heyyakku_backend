using Microsoft.EntityFrameworkCore;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence
{
    public class YakkuDbContext : DbContext
    {
        public YakkuDbContext(DbContextOptions<YakkuDbContext> options) : base(options)
        {
        }

        public DbSet<Poll> Polls => Set<Poll>();
        public DbSet<PollOption> PollOptions => Set<PollOption>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<UserAvatar> UserAvatars => Set<UserAvatar>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<Guest> Guests => Set<Guest>();
        public DbSet<Vote> Votes => Set<Vote>();
        public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
        public DbSet<EmailLog> EmailLogs => Set<EmailLog>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Image> Images => Set<Image>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(YakkuDbContext).Assembly);
        }
    }
}
