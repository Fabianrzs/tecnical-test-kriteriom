using Kriteriom.Notifications.Domain.Entities;

namespace Kriteriom.Notifications.Domain.Repositories;

public interface INotificationRepository
{
    Task<Guid> CreateAsync(NotificationRecord record, CancellationToken ct);
    Task MarkSentAsync(Guid id, CancellationToken ct);
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct);
    Task<bool> ExistsForEventAsync(Guid eventId, CancellationToken ct);
    Task<IReadOnlyList<NotificationRecord>> GetByCreditIdAsync(Guid creditId, CancellationToken ct);
    Task<(IReadOnlyList<NotificationRecord> Items, int Total)> GetPagedAsync(
        int page, int pageSize, NotificationStatus? status, CancellationToken ct);
}
