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
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<Guest> Guests => Set<Guest>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<UserActivityDay> UserActivityDays => Set<UserActivityDay>();
        public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
        public DbSet<AuthenticationAuditLog> AuthenticationAuditLogs => Set<AuthenticationAuditLog>();
        public DbSet<PollAuditLog> PollAuditLogs => Set<PollAuditLog>();
        public DbSet<NotificationAuditLog> NotificationAuditLogs => Set<NotificationAuditLog>();
        public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
        public DbSet<Check> Checks => Set<Check>();
        public DbSet<CheckOption> CheckOptions => Set<CheckOption>();
        public DbSet<UserCheck> UserChecks => Set<UserCheck>();
        public DbSet<UserCheckVote> UserCheckVotes => Set<UserCheckVote>();
        public DbSet<ShareCode> ShareCodes => Set<ShareCode>();
        public DbSet<ReasonReport> ReasonReports => Set<ReasonReport>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(YakkuDbContext).Assembly);
        }
    }
}
