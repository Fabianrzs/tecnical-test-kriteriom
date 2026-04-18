namespace Kriteriom.SharedKernel.Outbox;

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message, CancellationToken ct = default);
    Task<IEnumerable<OutboxMessage>> GetPendingAsync(int batchSize = 50, CancellationToken ct = default);
    Task MarkProcessedAsync(Guid id, CancellationToken ct = default);
    Task MarkFailedAsync(Guid id, string error, CancellationToken ct = default);
}
