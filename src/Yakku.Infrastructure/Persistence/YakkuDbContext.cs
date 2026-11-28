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
        public DbSet<SavedCard> SavedCards => Set<SavedCard>();
        public DbSet<CommunityWaitlistEntry> CommunityWaitlist => Set<CommunityWaitlistEntry>();
        public DbSet<CommunityQueuedQuestion> CommunityQueuedQuestions => Set<CommunityQueuedQuestion>();
        public DbSet<CardTag> CardTags => Set<CardTag>();
        public DbSet<CardCategory> CardCategories => Set<CardCategory>();
        public DbSet<User> Users => Set<User>();
        public DbSet<UserDeletionRequest> UserDeletionRequests => Set<UserDeletionRequest>();
        public DbSet<Plan> Plans => Set<Plan>();
        public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
        public DbSet<UserPlan> UserPlans => Set<UserPlan>();
        public DbSet<UserSession> UserSessions => Set<UserSession>();
        public DbSet<Guest> Guests => Set<Guest>();
        public DbSet<Device> Devices => Set<Device>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<UserActivityDay> UserActivityDays => Set<UserActivityDay>();
        public DbSet<UserStreak> UserStreaks => Set<UserStreak>();
        public DbSet<Admin> Admins => Set<Admin>();
        public DbSet<AdminPassword> AdminPasswords => Set<AdminPassword>();
        public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
        public DbSet<AuthenticationAuditLog> AuthenticationAuditLogs => Set<AuthenticationAuditLog>();
        public DbSet<PollAuditLog> PollAuditLogs => Set<PollAuditLog>();
        public DbSet<NotificationAuditLog> NotificationAuditLogs => Set<NotificationAuditLog>();
        public DbSet<AdminAuditLog> AdminAuditLogs => Set<AdminAuditLog>();
        public DbSet<ContentAuditLog> ContentAuditLogs => Set<ContentAuditLog>();
        public DbSet<EntitlementAuditLog> EntitlementAuditLogs => Set<EntitlementAuditLog>();
        public DbSet<SystemLog> SystemLogs => Set<SystemLog>();
        public DbSet<Check> Checks => Set<Check>();
        public DbSet<CardTheme> CardThemes => Set<CardTheme>();
        public DbSet<CheckOption> CheckOptions => Set<CheckOption>();
        public DbSet<UserCheck> UserChecks => Set<UserCheck>();
        public DbSet<UserCheckVote> UserCheckVotes => Set<UserCheckVote>();
        public DbSet<ShareCode> ShareCodes => Set<ShareCode>();
        public DbSet<ReasonReport> ReasonReports => Set<ReasonReport>();
        public DbSet<VoteNotificationCard> VoteNotificationCards => Set<VoteNotificationCard>();
        public DbSet<VoteNotificationEvent> VoteNotificationEvents => Set<VoteNotificationEvent>();
        public DbSet<VoteDigestWindow> VoteDigestWindows => Set<VoteDigestWindow>();
        public DbSet<AskBackConversation> AskBackConversations => Set<AskBackConversation>();
        public DbSet<AskBackParticipant> AskBackParticipants => Set<AskBackParticipant>();
        public DbSet<AskBackMessage> AskBackMessages => Set<AskBackMessage>();
        public DbSet<AskBackOutboxEvent> AskBackOutbox => Set<AskBackOutboxEvent>();
        public DbSet<AskBackReport> AskBackReports => Set<AskBackReport>();
        public DbSet<AskBackMessageDeliveryTracking> AskBackDeliveries => Set<AskBackMessageDeliveryTracking>();
        public DbSet<AskBackConversationTopUp> AskBackConversationTopUps => Set<AskBackConversationTopUp>();
        public DbSet<CardUnlock> CardUnlocks => Set<CardUnlock>();
        public DbSet<UserBlock> UserBlocks => Set<UserBlock>();
        public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
        public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
        public DbSet<UserPlanEntitlement> UserPlanEntitlements => Set<UserPlanEntitlement>();
        public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();
        public DbSet<PaymentLog> PaymentLogs => Set<PaymentLog>();
        public DbSet<PaymentRefund> PaymentRefunds => Set<PaymentRefund>();

        public DbSet<PersonalityTrait> PersonalityTraits => Set<PersonalityTrait>();
        public DbSet<PersonalityCard> PersonalityCards => Set<PersonalityCard>();
        public DbSet<PersonalityCardSelfTrait> PersonalityCardSelfTraits => Set<PersonalityCardSelfTrait>();
        public DbSet<PersonalityVote> PersonalityVotes => Set<PersonalityVote>();
        public DbSet<PersonalityVoteTrait> PersonalityVoteTraits => Set<PersonalityVoteTrait>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(YakkuDbContext).Assembly);
        }
    }
}
