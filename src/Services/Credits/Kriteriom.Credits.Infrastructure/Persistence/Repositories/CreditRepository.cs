using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Repositories;
using Kriteriom.Credits.Domain.Specifications;
using Kriteriom.Credits.Infrastructure.Persistence.Specifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Infrastructure.Persistence.Repositories;

public class CreditRepository(CreditsDbContext context, ILogger<CreditRepository> logger) : ICreditRepository
{
    public async Task<Credit?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Credits.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<(IEnumerable<Credit> Items, int Total)> GetAllAsync(
        int page,
        int pageSize,
        CreditStatus? status = null,
        Guid? clientId = null,
        CancellationToken ct = default)
    {
        var query = context.Credits
            .AsNoTracking()
            .Where(c => c.Amount > 0); 
        
        if (status.HasValue)
            query = query.Where(c => c.Status == status.Value);

        if (clientId.HasValue)
            query = query.Where(c => c.ClientId == clientId.Value);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("GetAllAsync page={Page} pageSize={PageSize} total={Total}", page, pageSize, total);

        return (items, total);
    }

    public async Task<IEnumerable<Credit>> GetBySpecificationAsync(ISpecification<Credit> spec, CancellationToken ct = default)
    {
        return await SpecificationEvaluator<Credit>
            .GetQuery(context.Credits.AsNoTracking(), spec)
            .ToListAsync(ct);
    }

    public async Task<int> CountBySpecificationAsync(ISpecification<Credit> spec, CancellationToken ct = default)
    {
        return await SpecificationEvaluator<Credit>
            .GetQuery(context.Credits.AsNoTracking(), spec)
            .CountAsync(ct);
    }

    public async Task AddAsync(Credit credit, CancellationToken ct = default)
        => await context.Credits.AddAsync(credit, ct);

    public Task UpdateAsync(Credit credit, CancellationToken ct = default)
    {
        context.Credits.Update(credit);
        return Task.CompletedTask;
    }

    public async Task<IEnumerable<Credit>> GetForBatchProcessingAsync(
        int batchSize,
        int skip,
        CancellationToken ct = default)
    {
        
        return await context.Credits
            .AsNoTracking()
            .OrderBy(c => c.CreatedAt)
            .Skip(skip)
            .Take(batchSize)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
    {
        return await context.Credits.CountAsync(ct);
    }

    public async Task<Dictionary<int, int>> GetStatusCountsAsync(CancellationToken ct = default)
    {
        return await context.Credits
            .GroupBy(c => (int)c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, ct);
    }

    public async Task<int> GetActiveCountForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        return await context.Credits
            .AsNoTracking()
            .CountAsync(c => c.ClientId == clientId && c.Status == CreditStatus.Active, ct);
    }

    public async Task<IEnumerable<Credit>> GetActiveCreditsForClientAsync(Guid clientId, CancellationToken ct = default)
    {
        return await context.Credits
            .AsNoTracking()
            .Where(c => c.ClientId == clientId && c.Status == CreditStatus.Active)
            .ToListAsync(ct);
    }
}
