using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Specifications;

namespace Kriteriom.Credits.Domain.Repositories;

public interface ICreditRepository
{
    Task<Credit?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<(IEnumerable<Credit> Items, int Total)> GetAllAsync(int page, int pageSize, CreditStatus? status = null, Guid? clientId = null, CancellationToken ct = default);
    Task<IEnumerable<Credit>> GetBySpecificationAsync(ISpecification<Credit> spec, CancellationToken ct = default);
    Task<int> CountBySpecificationAsync(ISpecification<Credit> spec, CancellationToken ct = default);
    Task AddAsync(Credit credit, CancellationToken ct = default);
    Task UpdateAsync(Credit credit, CancellationToken ct = default);
    Task<IEnumerable<Credit>> GetForBatchProcessingAsync(int batchSize, int skip, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
    Task<Dictionary<int, int>> GetStatusCountsAsync(CancellationToken ct = default);
    Task<int> GetActiveCountForClientAsync(Guid clientId, CancellationToken ct = default);
    Task<IEnumerable<Credit>> GetActiveCreditsForClientAsync(Guid clientId, CancellationToken ct = default);
}
