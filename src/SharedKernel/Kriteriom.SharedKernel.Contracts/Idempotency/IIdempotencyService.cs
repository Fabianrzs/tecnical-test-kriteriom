namespace Kriteriom.SharedKernel.Contracts.Idempotency;

public interface IIdempotencyService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task SetAsync(string key, string response, TimeSpan? expiry = null, CancellationToken ct = default);
}
