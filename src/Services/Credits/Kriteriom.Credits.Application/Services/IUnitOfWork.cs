namespace Kriteriom.Credits.Application.Services;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<Task> operation, CancellationToken ct = default);
}
