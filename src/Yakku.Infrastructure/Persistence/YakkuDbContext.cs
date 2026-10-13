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
        public DbSet<PollMedia> PollMedia => Set<PollMedia>();
        public DbSet<PollVote> PollVotes => Set<PollVote>();
        public DbSet<MediaUploadSession> MediaUploadSessions => Set<MediaUploadSession>();
        public DbSet<SavedPoll> SavedPolls => Set<SavedPoll>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<Guest> Guests => Set<Guest>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<AdminSession> AdminSessions => Set<AdminSession>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(YakkuDbContext).Assembly);
        }
    }
}
