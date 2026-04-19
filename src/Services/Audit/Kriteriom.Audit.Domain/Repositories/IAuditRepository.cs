using Kriteriom.Audit.Domain.Entities;

namespace Kriteriom.Audit.Domain.Repositories;

public interface IAuditRepository
{
    Task AddAsync(AuditRecord record, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default);
    Task<IEnumerable<AuditRecord>> GetByEntityIdAsync(Guid entityId, int limit = 100, CancellationToken ct = default);
    Task<(IEnumerable<AuditRecord> Items, int Total)> GetRecentAsync(
        int       page,
        int       pageSize,
        string?   eventType = null,
        DateTime? dateFrom  = null,
        DateTime? dateTo    = null,
        Guid?     entityId  = null,
        CancellationToken ct = default);
}
