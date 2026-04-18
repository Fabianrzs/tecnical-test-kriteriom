using Kriteriom.Credits.Domain.Aggregates;
using Kriteriom.Credits.Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kriteriom.Credits.Infrastructure.Persistence.Repositories;

public class ClientRepository(CreditsDbContext context, ILogger<ClientRepository> logger) : IClientRepository
{
    public async Task<Client?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Client?> GetByEmailAsync(string email, CancellationToken ct = default)
        => await context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Email == email.ToLowerInvariant(), ct);

    public async Task<Client?> GetByDocumentAsync(string documentNumber, CancellationToken ct = default)
        => await context.Clients.AsNoTracking()
            .FirstOrDefaultAsync(c => c.DocumentNumber == documentNumber, ct);

    public async Task<(IEnumerable<Client> Items, int Total)> GetAllAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var query  = context.Clients.AsNoTracking().OrderBy(c => c.FullName);
        var total  = await query.CountAsync(ct);
        var items  = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        if (logger.IsEnabled(LogLevel.Debug))
            logger.LogDebug("GetAllAsync page={Page} pageSize={PageSize} total={Total}", page, pageSize, total);

        return (items, total);
    }

    public async Task AddAsync(Client client, CancellationToken ct = default)
    {
        await context.Clients.AddAsync(client, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        context.Clients.Update(client);
        await context.SaveChangesAsync(ct);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken ct = default)
        => await context.Clients.CountAsync(ct);
}
