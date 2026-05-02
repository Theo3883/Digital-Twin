using DigitalTwin.Domain.Enums;
using DigitalTwin.Domain.Interfaces.Repositories;
using DigitalTwin.Domain.Models;
using DigitalTwin.Infrastructure.Data;
using DigitalTwin.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalTwin.Infrastructure.Repositories;

public sealed class NotificationRepository : INotificationRepository
{
    private readonly Func<HealthAppDbContext> _factory;

    public NotificationRepository(Func<HealthAppDbContext> factory)
        => _factory = factory;

    public async Task AddAsync(NotificationItem notification)
    {
        await using var db = _factory();
        var entity = ToEntity(notification);
        db.Notifications.Add(entity);
        await db.SaveChangesAsync();
        notification.Id = entity.Id;
    }

    public async Task<IReadOnlyList<NotificationItem>> GetByRecipientAsync(
        Guid recipientUserId,
        UserRole role,
        bool unreadOnly,
        int limit)
    {
        await using var db = _factory();
        var query = db.Notifications
            .Where(n => n.RecipientUserId == recipientUserId && n.RecipientRole == (int)role);

        if (unreadOnly)
            query = query.Where(n => n.ReadAt == null);

        limit = Math.Clamp(limit, 1, 500);

        var entities = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();

        return entities.Select(ToDomain).ToList();
    }

    public async Task<NotificationItem?> GetByIdAsync(Guid id, Guid recipientUserId, UserRole role)
    {
        await using var db = _factory();
        var entity = await db.Notifications.FirstOrDefaultAsync(n =>
            n.Id == id
            && n.RecipientUserId == recipientUserId
            && n.RecipientRole == (int)role);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<int> GetUnreadCountAsync(Guid recipientUserId, UserRole role)
    {
        await using var db = _factory();
        return await db.Notifications.CountAsync(n =>
            n.RecipientUserId == recipientUserId
            && n.RecipientRole == (int)role
            && n.ReadAt == null);
    }

    public async Task MarkReadAsync(Guid id, DateTime readAtUtc)
    {
        await using var db = _factory();
        await db.Notifications
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, readAtUtc));
    }

    public async Task MarkAllReadAsync(Guid recipientUserId, UserRole role, DateTime readAtUtc)
    {
        await using var db = _factory();
        await db.Notifications
            .Where(n => n.RecipientUserId == recipientUserId
                        && n.RecipientRole == (int)role
                        && n.ReadAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAt, readAtUtc));
    }

    public async Task SoftDeleteAsync(Guid id, DateTime deletedAtUtc)
    {
        await using var db = _factory();
        await db.Notifications
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.DeletedAt, deletedAtUtc));
    }

    private static NotificationItem ToDomain(NotificationEntity entity) => new()
    {
        Id = entity.Id,
        RecipientUserId = entity.RecipientUserId,
        RecipientRole = (UserRole)entity.RecipientRole,
        Title = entity.Title,
        Body = entity.Body,
        Type = (NotificationType)entity.Type,
        Severity = (NotificationSeverity)entity.Severity,
        PatientId = entity.PatientId,
        ActorUserId = entity.ActorUserId,
        ActorName = entity.ActorName,
        CreatedAt = entity.CreatedAt,
        ReadAt = entity.ReadAt,
        DeletedAt = entity.DeletedAt
    };

    private static NotificationEntity ToEntity(NotificationItem model) => new()
    {
        Id = model.Id == Guid.Empty ? Guid.NewGuid() : model.Id,
        RecipientUserId = model.RecipientUserId,
        RecipientRole = (int)model.RecipientRole,
        Title = model.Title,
        Body = model.Body,
        Type = (int)model.Type,
        Severity = (int)model.Severity,
        PatientId = model.PatientId,
        ActorUserId = model.ActorUserId,
        ActorName = model.ActorName,
        CreatedAt = model.CreatedAt,
        ReadAt = model.ReadAt,
        DeletedAt = model.DeletedAt
    };
}
