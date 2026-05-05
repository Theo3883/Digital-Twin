using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Models;

namespace DigitalTwin.Domain.Interfaces.Repositories;

public interface INotificationRepository
{
    Task AddAsync(NotificationItem notification);
    Task<IReadOnlyList<NotificationItem>> GetByRecipientAsync(
        Guid recipientUserId,
        UserRole role,
        bool unreadOnly,
        int limit);
    Task<NotificationItem?> GetByIdAsync(Guid id, Guid recipientUserId, UserRole role);
    Task<int> GetUnreadCountAsync(Guid recipientUserId, UserRole role);
    Task MarkReadAsync(Guid id, DateTime readAtUtc);
    Task MarkAllReadAsync(Guid recipientUserId, UserRole role, DateTime readAtUtc);
    Task SoftDeleteAsync(Guid id, DateTime deletedAtUtc);
}
