using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Admin
    {
        public Guid Id { get; private set; }
        public string Email { get; private set; } = string.Empty;
        public string PasswordHash { get; private set; } = string.Empty;
        public AdminRole Role { get; private set; }
        public AdminStatus Status { get; private set; }
        public bool IsVerified { get; private set; }
        public DateTime? VerifiedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastLoginAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public ICollection<AdminSession> Sessions { get; private set; } = new List<AdminSession>();
        public ICollection<AuditLog> AuditLogs { get; private set; } = new List<AuditLog>();
        public ICollection<Category> DeletedCategories { get; private set; } = new List<Category>();
        public ICollection<Report> ReviewedReports { get; private set; } = new List<Report>();
        public ICollection<SystemLog> SystemLogs { get; private set; } = new List<SystemLog>();

        private Admin()
        {
        }

        public Admin(string email, string passwordHash, AdminRole role)
        {
            Id = Guid.NewGuid();
            Email = email;
            PasswordHash = passwordHash;
            Role = role;
            Status = AdminStatus.Pending;
            IsVerified = false;
            CreatedAt = DateTime.UtcNow;
            UpdatedAt = CreatedAt;
        }

        public void UpdatePassword(string passwordHash)
        {
            if (string.IsNullOrWhiteSpace(passwordHash))
            {
                throw new ArgumentException("Password hash is required.", nameof(passwordHash));
            }

            PasswordHash = passwordHash;
            UpdatedAt = DateTime.UtcNow;
        }

        public void MarkVerified()
        {
            var now = DateTime.UtcNow;
            IsVerified = true;
            VerifiedAt ??= now;
            Status = AdminStatus.Active;
            UpdatedAt = now;
        }

        public void RecordLogin()
        {
            LastLoginAt = DateTime.UtcNow;
            UpdatedAt = LastLoginAt.Value;
        }
    }
}
