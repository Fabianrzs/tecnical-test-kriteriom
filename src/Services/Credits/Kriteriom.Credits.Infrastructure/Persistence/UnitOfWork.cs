using Kriteriom.Credits.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace Kriteriom.Credits.Infrastructure.Persistence;

public class UnitOfWork(CreditsDbContext context) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
        => context.SaveChangesAsync(ct);

    public Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async innerCt =>
        {
            await using var tx = await context.Database.BeginTransactionAsync(innerCt);
            try
            {
                await operation();
                await context.SaveChangesAsync(innerCt);
                await tx.CommitAsync(innerCt);
            }
            catch
            {
                await tx.RollbackAsync(innerCt);
                throw;
            }
        }, ct);
    }
}
