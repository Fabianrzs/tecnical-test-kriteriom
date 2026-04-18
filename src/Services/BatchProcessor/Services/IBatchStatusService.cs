using Kriteriom.BatchProcessor.Persistence;

namespace Kriteriom.BatchProcessor.Services;

public interface IBatchStatusService
{
    Task<IReadOnlyList<BatchJobCheckpoint>> GetCheckpointsAsync(CancellationToken ct);
}
