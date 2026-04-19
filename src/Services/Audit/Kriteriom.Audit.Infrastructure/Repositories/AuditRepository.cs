using Kriteriom.Audit.Domain.Entities;
using Kriteriom.Audit.Domain.Repositories;
using Kriteriom.Audit.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Audit.Infrastructure.Repositories;

public class AuditRepository(AuditDbContext dbContext, ILogger<AuditRepository> logger) : IAuditRepository
{
    public async Task AddAsync(AuditRecord record, CancellationToken ct = default)
    {
        if (await ExistsAsync(record.EventId, ct))
        {
            logger.LogWarning(
                "Duplicate audit record skipped. EventId={EventId}, EventType={EventType}",
                record.EventId, record.EventType);
            return;
        }

        await dbContext.AuditRecords.AddAsync(record, ct);
        await dbContext.SaveChangesAsync(ct);

        logger.LogDebug(
            "Audit record saved. Id={Id}, EventId={EventId}, EventType={EventType}, EntityId={EntityId}",
            record.Id, record.EventId, record.EventType, record.EntityId);
    }

    public async Task<bool> ExistsAsync(Guid eventId, CancellationToken ct = default) =>
        await dbContext.AuditRecords.AnyAsync(r => r.EventId == eventId, ct);

    public async Task<IEnumerable<AuditRecord>> GetByEntityIdAsync(
        Guid entityId, int limit = 100, CancellationToken ct = default) =>
        await dbContext.AuditRecords
            .Where(r => r.EntityId == entityId)
            .OrderByDescending(r => r.OccurredOn)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<(IEnumerable<AuditRecord> Items, int Total)> GetRecentAsync(
        int       page,
        int       pageSize,
        string?   eventType = null,
        DateTime? dateFrom  = null,
        DateTime? dateTo    = null,
        Guid?     entityId  = null,
        CancellationToken ct = default)
    {
        var query = dbContext.AuditRecords.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(r => r.EventType == eventType);

        if (dateFrom.HasValue)
            query = query.Where(r => r.OccurredOn >= dateFrom.Value.ToUniversalTime());

        if (dateTo.HasValue)
            query = query.Where(r => r.OccurredOn <= dateTo.Value.ToUniversalTime().AddDays(1));

        if (entityId.HasValue)
            query = query.Where(r => r.EntityId == entityId.Value);

        query = query.OrderByDescending(r => r.OccurredOn);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return (items, total);
    }
}
