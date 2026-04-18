using Kriteriom.BatchProcessor.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kriteriom.BatchProcessor.Services;

public class BatchStatusService(BatchDbContext dbContext) : IBatchStatusService
{
    public async Task<IReadOnlyList<BatchJobCheckpoint>> GetCheckpointsAsync(CancellationToken ct) =>
        await dbContext.BatchJobCheckpoints
            .OrderByDescending(c => c.StartedAt)
            .AsNoTracking()
            .ToListAsync(ct);
}
