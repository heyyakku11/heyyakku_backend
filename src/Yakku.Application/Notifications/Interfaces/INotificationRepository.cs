using Yakku.Domain.Entities;

namespace Yakku.Application.Notifications.Interfaces
{
    public interface INotificationRepository
    {
        Task<IReadOnlyList<Notification>> GetByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default);

        Task<Notification?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>How many notifications of the user are unread.</summary>
        Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Marks every unread notification of the user as read; returns how many changed.</summary>
        Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);

        Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
