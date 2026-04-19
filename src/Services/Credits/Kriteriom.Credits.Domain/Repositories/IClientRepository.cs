using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;

namespace Kriteriom.Credits.Domain.Repositories;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Client?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<Client?> GetByDocumentAsync(string documentNumber, CancellationToken ct = default);
    Task<(IEnumerable<Client> Items, int Total)> GetAllAsync(
        int             page,
        int             pageSize,
        string?         search           = null,
        EmploymentStatus? employmentStatus = null,
        string?         scoreTier        = null,
        decimal?        incomeMin        = null,
        decimal?        incomeMax        = null,
        CancellationToken ct = default);
    Task AddAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task<int> GetTotalCountAsync(CancellationToken ct = default);
}
