using Kriteriom.Notifications.Domain.Entities;
using Kriteriom.Notifications.Domain.Repositories;
using Kriteriom.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kriteriom.Notifications.Infrastructure.Repositories;

public class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public async Task<Guid> CreateAsync(NotificationRecord record, CancellationToken ct)
    {
        record.Id        = Guid.NewGuid();
        record.CreatedAt = DateTime.UtcNow;
        record.Status    = NotificationStatus.Pending;
        db.Notifications.Add(record);
        await db.SaveChangesAsync(ct);
        return record.Id;
    }

    public async Task MarkSentAsync(Guid id, CancellationToken ct) =>
        await db.Notifications
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Status, NotificationStatus.Sent)
                .SetProperty(n => n.SentAt, DateTime.UtcNow), ct);

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct) =>
        await db.Notifications
            .Where(n => n.Id == id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(n => n.Status, NotificationStatus.Failed)
                .SetProperty(n => n.ErrorMessage, error)
                .SetProperty(n => n.RetryCount, n => n.RetryCount + 1), ct);

    public async Task<bool> ExistsForEventAsync(Guid eventId, CancellationToken ct) =>
        await db.Notifications.AnyAsync(n => n.EventId == eventId, ct);

    public async Task<IReadOnlyList<NotificationRecord>> GetByCreditIdAsync(Guid creditId, CancellationToken ct) =>
        await db.Notifications
            .Where(n => n.CreditId == creditId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<NotificationRecord> Items, int Total)> GetPagedAsync(
        int page, int pageSize, NotificationStatus? status, CancellationToken ct)
    {
        var query = db.Notifications.AsQueryable();
        if (status.HasValue)
            query = query.Where(n => n.Status == status.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, total);
    }
}
