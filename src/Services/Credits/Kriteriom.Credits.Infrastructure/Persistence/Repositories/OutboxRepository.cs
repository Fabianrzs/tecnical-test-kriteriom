using Kriteriom.SharedKernel.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Infrastructure.Persistence.Repositories;

public class OutboxRepository(CreditsDbContext context, ILogger<OutboxRepository> logger) : IOutboxRepository
{
    public async Task AddAsync(OutboxMessage message, CancellationToken ct = default)
        => await context.OutboxMessages.AddAsync(message, ct);

    public async Task<IEnumerable<OutboxMessage>> GetPendingAsync(int batchSize = 50, CancellationToken ct = default)
    {
        return await context.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task MarkProcessedAsync(Guid id, CancellationToken ct = default)
    {
        var message = await context.OutboxMessages.FindAsync([id], ct);
        if (message is null)
        {
            logger.LogWarning("Outbox message {MessageId} not found when marking as processed", id);
            return;
        }

        message.ProcessedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
    }

    public async Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default)
    {
        var message = await context.OutboxMessages.FindAsync([id], ct);
        if (message is null)
        {
            logger.LogWarning("Outbox message {MessageId} not found when marking as failed", id);
            return;
        }

        message.RetryCount++;
        message.Error = error.Length > 2000 ? error[..2000] : error;
        await context.SaveChangesAsync(ct);
    }
}
