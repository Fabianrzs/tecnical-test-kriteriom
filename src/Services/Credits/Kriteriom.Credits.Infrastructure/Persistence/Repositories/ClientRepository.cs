using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Enums;
using Kriteriom.Credits.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Infrastructure.Persistence.Repositories;

public class ClientRepository(CreditsDbContext context, ILogger<ClientRepository> logger) : IClientRepository
{
    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Client?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant(), ct);

    public async Task<Client?> GetByDocumentAsync(string documentNumber, CancellationToken ct = default)
        => await context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.DocumentNumber == documentNumber, ct);

    public async Task<(IEnumerable<Client> Items, int Total)> GetAllAsync(
        int               page,
        int               pageSize,
        string?           search           = null,
        EmploymentStatus? employmentStatus = null,
        string?           scoreTier        = null,
        decimal?          incomeMin        = null,
        decimal?          incomeMax        = null,
        CancellationToken ct = default)
    {
        var query = context.Clients.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c =>
                EF.Functions.ILike(c.FullName,       $"%{search}%") ||
                EF.Functions.ILike(c.DocumentNumber, $"%{search}%") ||
                EF.Functions.ILike(c.Email,          $"%{search}%"));

        if (employmentStatus.HasValue)
            query = query.Where(c => c.EmploymentStatus == employmentStatus.Value);

        if (!string.IsNullOrWhiteSpace(scoreTier))
            query = scoreTier.ToLowerInvariant() switch
            {
                "good"    => query.Where(c => c.CreditScore >= 700),
                "regular" => query.Where(c => c.CreditScore >= 550 && c.CreditScore < 700),
                "low"     => query.Where(c => c.CreditScore < 550),
                _         => query
            };

        if (incomeMin.HasValue)
            query = query.Where(c => c.MonthlyIncome >= incomeMin.Value);

        if (incomeMax.HasValue)
            query = query.Where(c => c.MonthlyIncome <= incomeMax.Value);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("GetAllAsync page={Page} size={PageSize} total={Total}", page, pageSize, total);

        return (items, total);
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
        => await context.Clients.AddAsync(client, ct);

    public Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        context.Clients.Update(client);
        return Task.CompletedTask;
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await context.Clients.CountAsync(ct);
}
